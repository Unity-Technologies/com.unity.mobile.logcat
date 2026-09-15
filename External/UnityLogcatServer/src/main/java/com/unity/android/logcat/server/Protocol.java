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
 * Stream header, once, 12 bytes:
 *   u32  magic            'U' 'L' 'S' '1' (0x554C5331)
 *   u32  protocolVersion  BuildConfig.PROTOCOL_VERSION
 *   u32  codec            CODEC_MJPEG
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

    private final DataOutputStream out;
    private long firstFrameNs = -1;

    public Protocol(OutputStream stream) {
        this.out = new DataOutputStream(new BufferedOutputStream(stream, 64 * 1024));
    }

    public void writeStreamHeader(int codec) throws IOException {
        out.writeInt(MAGIC);
        out.writeInt(BuildConfig.PROTOCOL_VERSION);
        out.writeInt(codec);
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
