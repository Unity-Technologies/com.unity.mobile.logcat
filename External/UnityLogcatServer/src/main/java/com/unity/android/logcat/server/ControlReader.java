package com.unity.android.logcat.server;

import java.io.DataInputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;

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
 * </pre>
 *
 * Positions are normalized so that the Editor does not have to know the device's
 * current resolution - see {@link TouchInjector}.
 */
public final class ControlReader implements Runnable {
    public static final int TYPE_TOUCH = 1;

    private static final int NORMALIZED_MAX = 65535;

    private final InputStream input;
    private final TouchInjector touchInjector;
    private final Runnable onDisconnect;

    public ControlReader(InputStream input, TouchInjector touchInjector, Runnable onDisconnect) {
        this.input = input;
        this.touchInjector = touchInjector;
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

            if (type == TYPE_TOUCH) {
                readTouch(in);
            } else {
                // Every message has a fixed size, so an unknown type means we no longer
                // know where the next one starts. Reading on would inject garbage;
                // stopping leaves the video stream running, which is the useful half.
                Logger.w("Unknown control message type " + type + ", ignoring the rest of the control channel");
                return;
            }
        }
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
}
