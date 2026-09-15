package com.unity.android.logcat.server;

import com.unity.android.logcat.server.wrappers.DisplayManagerWrapper;
import com.unity.android.logcat.server.wrappers.SurfaceControlWrapper;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.PixelFormat;
import android.graphics.Rect;
import android.hardware.display.VirtualDisplay;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.IBinder;
import android.view.Surface;

import java.io.ByteArrayOutputStream;
import java.io.Closeable;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * Mirrors a display into an {@link ImageReader}, encodes each frame as JPEG and
 * writes it to the socket.
 * <p>
 * JPEG rather than H.264 is a deliberate trade. H.264 through {@code MediaCodec}
 * would cost a fraction of the bandwidth, but the Editor would then need a video
 * decoder, and there is no H.264 decoder reachable from Editor C#. A JPEG frame
 * goes straight into {@code Texture2D.LoadImage}, so the Editor side stays
 * dependency-free. At 1024px and quality 70 a frame is roughly 40-120 KB, i.e.
 * 1-4 MB/s at 30 fps, which fits comfortably in what adb forwards.
 */
public final class ScreenStreamer implements Closeable {
    private static final String VIRTUAL_DISPLAY_NAME = "unity-logcat";
    /**
     * Two buffers: one being mirrored into, one being encoded. More would only
     * buy latency.
     */
    private static final int MAX_IMAGES = 2;
    /** How often the display is re-read to notice a rotation or a resize. */
    private static final long DISPLAY_POLL_MS = 500;
    private static final long STATS_INTERVAL_NS = 5L * 1000 * 1000 * 1000;

    private final Options options;
    private final Protocol protocol;
    private final DisplayManagerWrapper displayManager;
    private final long minFrameIntervalNs;

    /** Guards the capture session, so a restart cannot race a frame callback. */
    private final Object sessionLock = new Object();
    private final CountDownLatch streamEnded = new CountDownLatch(1);

    private HandlerThread handlerThread;
    private Handler handler;

    private ImageReader imageReader;
    private VirtualDisplay virtualDisplay;
    private IBinder surfaceControlDisplay;
    private Size videoSize;

    private long lastFrameNs;
    private Bitmap paddedBitmap;
    private Bitmap frameBitmap;
    private Canvas frameCanvas;
    private final JpegBuffer jpeg = new JpegBuffer();

    private long statsStartNs;
    private int statsFrames;
    private long statsBytes;

    private volatile boolean stopped;
    private volatile IOException streamError;

    public ScreenStreamer(Options options, Protocol protocol, DisplayManagerWrapper displayManager) {
        this.options = options;
        this.protocol = protocol;
        this.displayManager = displayManager;
        this.minFrameIntervalNs = 1000000000L / options.getMaxFps();
    }

    /**
     * Streams until the client disconnects, the display disappears or
     * {@link #close()} is called.
     */
    public void stream() throws IOException, ReflectiveOperationException, InterruptedException {
        DisplayInfo info = readDisplayInfo();
        Logger.i("Capturing " + info);

        handlerThread = new HandlerThread("unity-logcat-capture");
        handlerThread.start();
        handler = new Handler(handlerThread.getLooper());

        statsStartNs = System.nanoTime();
        startSession(info);

        Size lastSize = info.getSize();
        int lastRotation = info.getRotation();

        while (!stopped) {
            if (streamEnded.await(DISPLAY_POLL_MS, TimeUnit.MILLISECONDS)) {
                break;
            }

            DisplayInfo current = displayManager.getDisplayInfo(options.getDisplayId());
            if (current == null) {
                Logger.w("Display " + options.getDisplayId() + " is gone, stopping");
                break;
            }

            // Rotating the device changes the logical size, which means a new
            // ImageReader and a new mirrored display. The client needs no warning:
            // every frame carries its own dimensions.
            if (!current.getSize().equals(lastSize) || current.getRotation() != lastRotation) {
                Logger.d("Display changed to " + current + ", restarting capture session");
                lastSize = current.getSize();
                lastRotation = current.getRotation();
                stopSession();
                startSession(current);
            }
        }

        if (streamError != null) {
            throw streamError;
        }
    }

    private DisplayInfo readDisplayInfo() throws ReflectiveOperationException, IOException {
        DisplayInfo info = displayManager.getDisplayInfo(options.getDisplayId());
        if (info == null) {
            StringBuilder available = new StringBuilder();
            for (int id : displayManager.getDisplayIds()) {
                available.append(' ').append(id);
            }
            throw new IOException("Unknown display id " + options.getDisplayId() + ", available:" + available);
        }
        return info;
    }

    private void startSession(DisplayInfo info) throws IOException {
        synchronized (sessionLock) {
            videoSize = info.getSize().limit(options.getMaxSize());
            int width = videoSize.getWidth();
            int height = videoSize.getHeight();

            imageReader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, MAX_IMAGES);
            imageReader.setOnImageAvailableListener(this::onImageAvailable, handler);
            Surface surface = imageReader.getSurface();

            try {
                virtualDisplay = displayManager
                    .createVirtualDisplay(VIRTUAL_DISPLAY_NAME, width, height, info.getDisplayId(), surface);
                Logger.d("Mirroring " + info.getSize() + " to " + videoSize + " via DisplayManagerGlobal");
            } catch (Exception displayManagerFailure) {
                // Expected on some devices and Android versions - the fallback is
                // the normal path there, so this is not a warning.
                Logger.d("DisplayManagerGlobal.createVirtualDisplay unavailable (" + displayManagerFailure
                    + "), falling back to SurfaceControl");
                try {
                    startSessionWithSurfaceControl(info, surface, width, height);
                    Logger.d("Mirroring " + info.getSize() + " to " + videoSize + " via SurfaceControl");
                } catch (Exception surfaceControlFailure) {
                    Logger.e("DisplayManagerGlobal.createVirtualDisplay failed", displayManagerFailure);
                    Logger.e("SurfaceControl.createDisplay failed", surfaceControlFailure);
                    imageReader.close();
                    imageReader = null;
                    throw new IOException("Could not mirror display " + info.getDisplayId());
                }
            }

            lastFrameNs = 0;
        }
    }

    private void startSessionWithSurfaceControl(DisplayInfo info, Surface surface, int width, int height)
            throws ReflectiveOperationException {
        surfaceControlDisplay = SurfaceControlWrapper.createDisplay(VIRTUAL_DISPLAY_NAME, false);
        SurfaceControlWrapper.openTransaction();
        try {
            SurfaceControlWrapper.setDisplaySurface(surfaceControlDisplay, surface);
            SurfaceControlWrapper.setDisplayProjection(surfaceControlDisplay, 0,
                new Rect(0, 0, info.getSize().getWidth(), info.getSize().getHeight()),
                new Rect(0, 0, width, height));
            SurfaceControlWrapper.setDisplayLayerStack(surfaceControlDisplay, info.getLayerStack());
        } finally {
            SurfaceControlWrapper.closeTransaction();
        }
    }

    private void stopSession() {
        synchronized (sessionLock) {
            if (virtualDisplay != null) {
                virtualDisplay.release();
                virtualDisplay = null;
            }
            if (surfaceControlDisplay != null) {
                try {
                    SurfaceControlWrapper.destroyDisplay(surfaceControlDisplay);
                } catch (ReflectiveOperationException e) {
                    Logger.w("Could not destroy SurfaceControl display", e);
                }
                surfaceControlDisplay = null;
            }
            if (imageReader != null) {
                imageReader.setOnImageAvailableListener(null, null);
                imageReader.close();
                imageReader = null;
            }
        }
    }

    private void onImageAvailable(ImageReader reader) {
        synchronized (sessionLock) {
            // A callback queued before the session was torn down.
            if (stopped || reader != imageReader) {
                return;
            }

            Image image = null;
            try {
                image = reader.acquireLatestImage();
                if (image == null) {
                    return;
                }

                long now = System.nanoTime();
                long waitNs = lastFrameNs == 0 ? 0 : minFrameIntervalNs - (now - lastFrameNs);
                if (waitNs > 0) {
                    // Wait rather than drop: on a screen that has stopped changing
                    // this may be the last frame produced for a long time, and
                    // dropping it would leave the client showing a stale image.
                    TimeUnit.NANOSECONDS.sleep(waitNs);
                    now = System.nanoTime();
                }
                lastFrameNs = now;

                encodeAndSend(image, now);
                reportStats(now);
            } catch (IOException e) {
                // The only IOException reachable here comes from writing to the
                // socket, which means the client is gone. That is how a session
                // normally ends, so it is not recorded as a stream error - doing so
                // would make an ordinary stop exit non-zero whenever the capture
                // thread noticed the disconnect before the watch thread did.
                if (!stopped) {
                    Logger.d("Client is gone (" + e + "), ending stream");
                }
                endStream();
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                endStream();
            } catch (RuntimeException e) {
                Logger.e("Unexpected failure while encoding a frame", e);
                streamError = new IOException("Frame encoding failed", e);
                endStream();
            } finally {
                if (image != null) {
                    image.close();
                }
            }
        }
    }

    private void encodeAndSend(Image image, long captureNs) throws IOException {
        Image.Plane plane = image.getPlanes()[0];
        ByteBuffer buffer = plane.getBuffer();
        int pixelStride = plane.getPixelStride();
        int rowStride = plane.getRowStride();

        int width = image.getWidth();
        int height = image.getHeight();
        // The plane's rows can be wider than the image; those extra pixels have to
        // be copied in and then cropped away.
        int paddedWidth = rowStride / pixelStride;

        Bitmap padded = obtainPaddedBitmap(paddedWidth, height);
        buffer.rewind();
        int required = padded.getRowBytes() * height;
        if (buffer.remaining() < required) {
            Logger.w("Frame plane is " + buffer.remaining() + " bytes, expected " + required + "; skipping frame");
            return;
        }
        padded.copyPixelsFromBuffer(buffer);

        Bitmap toEncode;
        if (paddedWidth == width) {
            toEncode = padded;
        } else {
            toEncode = obtainFrameBitmap(width, height);
            frameCanvas.drawBitmap(padded, 0, 0, null);
        }

        jpeg.reset();
        if (!toEncode.compress(Bitmap.CompressFormat.JPEG, options.getQuality(), jpeg)) {
            // Keeps IOException in this method meaning "the socket died", so that a
            // one-off encoder hiccup drops a frame instead of ending the session.
            Logger.e("JPEG encoding failed, skipping frame");
            return;
        }

        protocol.writeFrame(captureNs, width, height, jpeg.buffer(), jpeg.size());

        statsFrames++;
        statsBytes += jpeg.size();
    }

    private Bitmap obtainPaddedBitmap(int width, int height) {
        if (paddedBitmap == null || paddedBitmap.getWidth() != width || paddedBitmap.getHeight() != height) {
            if (paddedBitmap != null) {
                paddedBitmap.recycle();
            }
            paddedBitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
        }
        return paddedBitmap;
    }

    private Bitmap obtainFrameBitmap(int width, int height) {
        if (frameBitmap == null || frameBitmap.getWidth() != width || frameBitmap.getHeight() != height) {
            if (frameBitmap != null) {
                frameBitmap.recycle();
            }
            frameBitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            frameCanvas = new Canvas(frameBitmap);
        }
        return frameBitmap;
    }

    private void reportStats(long now) {
        if (!Logger.isEnabled(Logger.Level.DEBUG)) {
            return;
        }
        long elapsedNs = now - statsStartNs;
        if (elapsedNs < STATS_INTERVAL_NS) {
            return;
        }
        double seconds = elapsedNs / 1000000000.0;
        Logger.d(String.format("%s: %.1f fps, %.2f Mbps",
            videoSize, statsFrames / seconds, statsBytes * 8 / seconds / 1000000.0));
        statsStartNs = now;
        statsFrames = 0;
        statsBytes = 0;
    }

    private void endStream() {
        stopped = true;
        streamEnded.countDown();
    }

    @Override
    public synchronized void close() {
        endStream();

        HandlerThread thread = handlerThread;
        if (thread != null) {
            // Drops queued frame callbacks; a callback already running finishes,
            // which it will do promptly since the socket is closed by now.
            thread.quitSafely();
        }

        stopSession();

        if (thread != null) {
            try {
                thread.join(2000);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            handlerThread = null;
            handler = null;
        }

        // Safe now: only the capture thread touched these, and it has stopped.
        if (paddedBitmap != null) {
            paddedBitmap.recycle();
            paddedBitmap = null;
        }
        if (frameBitmap != null) {
            frameBitmap.recycle();
            frameBitmap = null;
        }
        frameCanvas = null;
    }

    /**
     * {@link ByteArrayOutputStream} that hands out its backing array, so a frame
     * is not copied on its way from the JPEG encoder to the socket.
     */
    private static final class JpegBuffer extends ByteArrayOutputStream {
        JpegBuffer() {
            super(256 * 1024);
        }

        byte[] buffer() {
            return buf;
        }
    }
}
