package com.unity.android.logcat.server;

import java.io.BufferedOutputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.OutputStream;

/**
 * Wire format written to the socket. All integers are big endian, matching
 * {@link DataOutputStream}.
 *
 * <pre>
 * Stream header, once, 16 bytes:
 *   u32  magic            'U' 'L' 'S' '1' (0x554C5331)
 *   u32  protocolVersion  BuildConfig.PROTOCOL_VERSION
 *   u32  codec            CODEC_MJPEG
 *   u32  flags            FLAG_CONTROL_SUPPORTED if touch can be injected
 *
 * Frame, repeated, 20 byte header + payload:
 *   u64  ptsUs            microseconds since the first frame
 *   u32  width            pixels
 *   u32  height           pixels
 *   u32  payloadSize      bytes of encoded frame that follow
 *   u8[] payload
 * </pre>
 *
 * Width and height travel with every frame rather than only in the stream header
 * because they change when the device is rotated. The reader therefore never has
 * to be told out of band that the geometry moved - it just reads the next frame.
 */
public final class Protocol {
    public static final int MAGIC = 0x554C5331;
    public static final int CODEC_MJPEG = 1;

    /**
     * Set when the server can inject input, so the Editor can tell "control is off"
     * from "control is impossible on this device" and say so instead of quietly
     * dropping every touch.
     */
    public static final int FLAG_CONTROL_SUPPORTED = 1;

    private final DataOutputStream out;
    private long firstFrameNs = -1;

    public Protocol(OutputStream stream) {
        this.out = new DataOutputStream(new BufferedOutputStream(stream, 64 * 1024));
    }

    public void writeStreamHeader(int codec, int flags) throws IOException {
        out.writeInt(MAGIC);
        out.writeInt(BuildConfig.PROTOCOL_VERSION);
        out.writeInt(codec);
        out.writeInt(flags);
        out.flush();
    }

    public void writeFrame(long captureNs, int width, int height, byte[] payload, int payloadSize) throws IOException {
        if (firstFrameNs < 0) {
            firstFrameNs = captureNs;
        }
        out.writeLong((captureNs - firstFrameNs) / 1000L);
        out.writeInt(width);
        out.writeInt(height);
        out.writeInt(payloadSize);
        out.write(payload, 0, payloadSize);
        // Flushed per frame: this is a live stream, buffering a frame to fill the
        // buffer would just add latency.
        out.flush();
    }
}
