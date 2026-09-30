package com.unity.android.logcat.server;

import java.io.DataInputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;

/**
 * Reads control messages the Editor sends back up the video socket, and notices the
 * Editor going away.
 * <p>
 * This runs even when nothing is being written to the socket, which is what makes a
 * departed client detectable on a screen that has stopped changing: with no frames
 * being produced there is no write to fail, so EOF here is the only signal.
 *
 * <pre>
 * Message, 1 byte type then a fixed payload per type:
 *
 *   TYPE_TOUCH (1), 8 byte payload:
 *     u8   action     0 down, 1 up, 2 move, 3 cancel
 *     u8   pointerId  0 based
 *     u16  x          position across the display, 0..65535
 *     u16  y          position down the display, 0..65535
 *     u16  pressure   0..65535
 *
 *   TYPE_SCROLL (4), 8 byte payload:
 *     u16  x          position across the display, 0..65535
 *     u16  y          position down the display, 0..65535
 *     i16  hScroll    notches right, times SCROLL_SCALE
 *     i16  vScroll    notches away from the user, times SCROLL_SCALE
 * </pre>
 *
 * Positions are normalized so that the Editor does not have to know the device's
 * current resolution - see {@link TouchInjector}.
 */
public final class ControlReader implements Runnable {
    public static final int TYPE_TOUCH = 1;
    public static final int TYPE_KEY = 2;
    public static final int TYPE_TEXT = 3;
    public static final int TYPE_SCROLL = 4;

    private static final int NORMALIZED_MAX = 65535;
    /**
     * Scroll notches are sent as fixed point, so that a trackpad's fractions survive
     * the trip without the message needing a float in it. 256 leaves room for +-128
     * notches in a single message, which no mouse will ever produce.
     */
    private static final int SCROLL_SCALE = 256;
    /** Generous for a keystroke or a paste, small enough that a bad length cannot hurt. */
    private static final int MAX_TEXT_LENGTH = 4096;

    private final InputStream input;
    private final TouchInjector touchInjector;
    private final KeyInjector keyInjector;
    private final ScrollInjector scrollInjector;
    private final Runnable onDisconnect;

    public ControlReader(InputStream input, TouchInjector touchInjector, KeyInjector keyInjector,
            ScrollInjector scrollInjector, Runnable onDisconnect) {
        this.input = input;
        this.touchInjector = touchInjector;
        this.keyInjector = keyInjector;
        this.scrollInjector = scrollInjector;
        this.onDisconnect = onDisconnect;
    }

    @Override
    public void run() {
        try {
            readMessages();
        } catch (EOFException e) {
            Logger.d("Client went away");
        } catch (IOException e) {
            // The socket was closed, by the client or by our own shutdown. Same
            // conclusion either way.
            Logger.d("Control channel closed: " + e);
        } finally {
            onDisconnect.run();
        }
    }

    private void readMessages() throws IOException {
        DataInputStream in = new DataInputStream(input);

        while (true) {
            int type = in.read();
            if (type == -1) {
                throw new EOFException();
            }

            switch (type) {
                case TYPE_TOUCH:
                    readTouch(in);
                    break;
                case TYPE_KEY:
                    readKey(in);
                    break;
                case TYPE_TEXT:
                    readText(in);
                    break;
                case TYPE_SCROLL:
                    readScroll(in);
                    break;
                default:
                    // Message sizes are known per type, so an unknown type means we no
                    // longer know where the next one starts, and reading on would
                    // inject garbage. Draining rather than returning keeps the other
                    // thing this thread is for: noticing EOF, which on a screen that
                    // has stopped changing is the only sign the client has gone. Return
                    // here instead and the video stream would be left running with
                    // nobody watching it - and with nothing left to notice that.
                    Logger.w("Unknown control message type " + type
                        + ", ignoring the rest of the control channel");
                    drainUntilClientGoes(in);
                    return;
            }
        }
    }

    /**
     * Reads and discards everything the client sends until it goes away, which is
     * reported as {@link EOFException} exactly as a clean end of stream would be.
     */
    private void drainUntilClientGoes(DataInputStream in) throws IOException {
        byte[] scratch = new byte[256];
        while (in.read(scratch) != -1) {
            // Discarded on purpose: the stream cannot be resynchronized, but the
            // connection is still worth watching.
        }
        throw new EOFException();
    }

    private void readTouch(DataInputStream in) throws IOException {
        int action = in.readUnsignedByte();
        int pointerId = in.readUnsignedByte();
        int x = in.readUnsignedShort();
        int y = in.readUnsignedShort();
        int pressure = in.readUnsignedShort();

        if (touchInjector == null) {
            // Input injection was unavailable at startup; the Editor was told, but a
            // message already in flight can still turn up here.
            return;
        }

        touchInjector.inject(action, pointerId,
            x / (float)NORMALIZED_MAX,
            y / (float)NORMALIZED_MAX,
            pressure / (float)NORMALIZED_MAX);
    }

    private void readScroll(DataInputStream in) throws IOException {
        int x = in.readUnsignedShort();
        int y = in.readUnsignedShort();
        int hScroll = in.readShort();
        int vScroll = in.readShort();

        if (scrollInjector == null) {
            return;
        }

        scrollInjector.inject(
            x / (float)NORMALIZED_MAX,
            y / (float)NORMALIZED_MAX,
            hScroll / (float)SCROLL_SCALE,
            vScroll / (float)SCROLL_SCALE);
    }

    private void readKey(DataInputStream in) throws IOException {
        int action = in.readUnsignedByte();
        int keyCode = in.readInt();
        int metaState = in.readInt();

        if (keyInjector == null) {
            return;
        }
        keyInjector.injectKey(action, keyCode, metaState);
    }

    private void readText(DataInputStream in) throws IOException {
        int length = in.readUnsignedShort();
        if (length > MAX_TEXT_LENGTH) {
            // The payload still has to be consumed, or the stream desynchronizes.
            in.skipBytes(length);
            Logger.w("Ignoring a " + length + " byte text message, the limit is " + MAX_TEXT_LENGTH);
            return;
        }

        byte[] bytes = new byte[length];
        in.readFully(bytes);

        if (keyInjector == null) {
            return;
        }
        keyInjector.injectText(new String(bytes, StandardCharsets.UTF_8));
    }
}
