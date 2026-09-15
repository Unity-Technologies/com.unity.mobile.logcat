package com.unity.android.logcat.server;

import com.unity.android.logcat.server.wrappers.DisplayManagerWrapper;
import com.unity.android.logcat.server.wrappers.InputManagerWrapper;

import android.net.LocalServerSocket;
import android.net.LocalSocket;

import java.io.Closeable;
import java.io.IOException;
import java.io.InputStream;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Entry point of the on-device server.
 * <p>
 * It is started by the Editor as:
 *
 * <pre>
 * adb push unity-logcat-server.jar /data/local/tmp/
 * adb shell CLASSPATH=/data/local/tmp/unity-logcat-server.jar \
 *     app_process / com.unity.android.logcat.server.Server max_size=1024
 * </pre>
 *
 * and then reached over an abstract unix socket that the Editor forwards to a
 * local TCP port:
 *
 * <pre>
 * adb forward tcp:0 localabstract:unity_logcat_server
 * </pre>
 *
 * <h2>Why a socket and not stdout</h2>
 * Frames could be written to stdout and read from the {@code adb shell} process,
 * as {@code screenrecord} does. A socket is used instead because it is
 * bidirectional - the same connection can later carry input events from the
 * Editor back to the device - and because it keeps frame data off a stream that
 * also carries log output.
 */
public final class Server {
    private static final String USAGE = "Usage: app_process / com.unity.android.logcat.server.Server [key=value ...]\n"
        + "  socket_name=<name>        abstract unix socket to listen on\n"
        + "  display_id=<id>           display to capture (default 0)\n"
        + "  max_size=<pixels>         longest side of the stream, 0 for native (default 1024)\n"
        + "  quality=<1..100>          JPEG quality (default 70)\n"
        + "  max_fps=<1..240>          frame rate cap (default 30)\n"
        + "  connect_timeout_ms=<ms>   how long to wait for the Editor (default 10000, 0 waits forever)\n"
        + "  log_level=<verbose|debug|info|warn|error>";

    private Server() {
    }

    public static void main(String... args) {
        Thread.setDefaultUncaughtExceptionHandler((thread, throwable) ->
            Logger.e("Uncaught exception on thread " + thread.getName(), throwable));

        int exitCode = 0;
        try {
            Options options = Options.parse(args);
            Logger.setLevel(options.getLogLevel());
            Logger.d("Protocol version " + BuildConfig.PROTOCOL_VERSION + ", options: " + options);
            run(options);
            Logger.i("Stopped");
        } catch (IllegalArgumentException e) {
            Logger.e(e.getMessage());
            Logger.e(USAGE);
            exitCode = 2;
        } catch (Throwable t) {
            Logger.e("Server failed", t);
            exitCode = 1;
        }

        // app_process does not exit on its own while a Looper or a non-daemon
        // thread is alive, and the Editor is waiting for its adb shell to return.
        System.exit(exitCode);
    }

    private static void run(Options options) throws Exception {
        DisplayManagerWrapper displayManager = DisplayManagerWrapper.create();

        LocalServerSocket serverSocket = new LocalServerSocket(options.getSocketName());
        Logger.i("Listening on localabstract:" + options.getSocketName());

        LocalSocket socket = null;
        ScreenStreamer streamer = null;
        try {
            socket = accept(serverSocket, options.getConnectTimeoutMs());
            Logger.d("Client connected");

            Protocol protocol = new Protocol(socket.getOutputStream());

            streamer = new ScreenStreamer(options, protocol, displayManager);

            // Input injection is optional: a device that will not allow it still
            // streams fine, so a failure here is reported in the header rather than
            // taken as fatal.
            TouchInjector touchInjector = createTouchInjector(streamer, options.getDisplayId());
            int flags = touchInjector != null ? Protocol.FLAG_CONTROL_SUPPORTED : 0;

            // Sent before anything else: `adb forward` succeeds as soon as the
            // socket exists, so the header is what tells the Editor it is really
            // talking to a server of a version it understands.
            protocol.writeStreamHeader(Protocol.CODEC_MJPEG, flags);

            startControlReader(socket, streamer, touchInjector);
            streamer.stream();
        } finally {
            // Socket first: it unblocks a capture thread parked in a write, so
            // that closing the streamer does not have to wait for the timeout.
            closeQuietly(socket);
            if (streamer != null) {
                streamer.close();
            }
            closeQuietly(serverSocket);
        }
    }

    /**
     * Waits for the Editor to connect, giving up after {@code timeoutMs} so that a
     * server whose Editor died does not sit on the device forever. A timeout of 0
     * waits indefinitely.
     */
    private static LocalSocket accept(LocalServerSocket serverSocket, int timeoutMs) throws IOException {
        if (timeoutMs <= 0) {
            return serverSocket.accept();
        }

        // Whichever of the two paths wins this CAS decides the outcome, so a client
        // arriving exactly as the timeout expires cannot be half-accepted.
        AtomicBoolean decided = new AtomicBoolean();

        Thread watchdog = new Thread(() -> {
            try {
                Thread.sleep(timeoutMs);
            } catch (InterruptedException e) {
                return;
            }
            if (decided.compareAndSet(false, true)) {
                Logger.e("No client connected within " + timeoutMs + " ms, giving up");
                // Note: closing the server socket here would NOT unblock the
                // accept() below. On Linux, closing a file descriptor from another
                // thread does not interrupt an accept() already parked on it, so
                // that leaves the process wedged forever - the exact orphan this
                // timeout exists to prevent. Exiting is what actually works, and
                // nothing has been claimed yet that needs unwinding: no client, no
                // capture session, and the kernel reclaims the socket.
                System.exit(1);
            }
        }, "unity-logcat-accept-timeout");
        watchdog.setDaemon(true);
        watchdog.start();

        try {
            LocalSocket socket = serverSocket.accept();
            if (!decided.compareAndSet(false, true)) {
                // The watchdog got there first and the process is already exiting.
                closeQuietly(socket);
                throw new IOException("Client connected as the accept timeout expired");
            }
            return socket;
        } finally {
            watchdog.interrupt();
        }
    }

    private static TouchInjector createTouchInjector(ScreenStreamer streamer, int displayId) {
        try {
            InputManagerWrapper inputManager = InputManagerWrapper.create();
            return new TouchInjector(inputManager, streamer::getDisplaySize, displayId);
        } catch (ReflectiveOperationException | RuntimeException e) {
            Logger.w("Input injection is unavailable, the stream will be view-only", e);
            return null;
        }
    }

    /**
     * Reads control messages from the Editor, and notices it going away.
     * <p>
     * Reading is also what detects a disconnect while the screen is static: with no
     * frames being produced there is no write to fail, so EOF here is the only signal.
     */
    private static void startControlReader(LocalSocket socket, ScreenStreamer streamer, TouchInjector touchInjector)
            throws IOException {
        InputStream input = socket.getInputStream();
        Thread thread = new Thread(new ControlReader(input, touchInjector, streamer::close),
            "unity-logcat-control");
        thread.setDaemon(true);
        thread.start();
    }

    private static void closeQuietly(Closeable closeable) {
        if (closeable == null) {
            return;
        }
        try {
            closeable.close();
        } catch (IOException e) {
            Logger.v("Ignoring close failure: " + e);
        }
    }
}
