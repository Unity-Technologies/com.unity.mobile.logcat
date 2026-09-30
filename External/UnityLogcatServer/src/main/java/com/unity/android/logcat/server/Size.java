package com.unity.android.logcat.server;

/** Immutable width/height pair. */
public final class Size {
    /**
     * Captured dimensions are rounded down to a multiple of this. Matching the
     * buffer alignment the graphics stack wants keeps {@code Image.Plane} row
     * padding at zero on most devices, which lets a frame be turned into a
     * {@link android.graphics.Bitmap} without an intermediate copy.
     */
    private static final int ALIGNMENT = 8;

    private final int width;
    private final int height;

    public Size(int width, int height) {
        this.width = width;
        this.height = height;
    }

    public int getWidth() {
        return width;
    }

    public int getHeight() {
        return height;
    }

    /**
     * Maps a normalized horizontal position, 0..1, onto these pixels. Clamped rather
     * than rejected: a drag that runs off the edge of the view in the Editor should
     * still read as a swipe to the edge of the screen.
     */
    public float pixelX(float normalized) {
        return clamp01(normalized) * width;
    }

    /** The same down the display. */
    public float pixelY(float normalized) {
        return clamp01(normalized) * height;
    }

    private static float clamp01(float value) {
        if (value < 0f) {
            return 0f;
        }
        return value > 1f ? 1f : value;
    }

    /**
     * Scales down so that the longest side is at most {@code maxSize}, preserving
     * aspect ratio. A {@code maxSize} of 0 means "do not scale", but the result is
     * aligned either way.
     */
    public Size limit(int maxSize) {
        int w = width;
        int h = height;

        if (maxSize > 0 && (w > maxSize || h > maxSize)) {
            if (w > h) {
                h = h * maxSize / w;
                w = maxSize;
            } else {
                w = w * maxSize / h;
                h = maxSize;
            }
        }

        return new Size(align(w), align(h));
    }

    private static int align(int value) {
        int aligned = value & ~(ALIGNMENT - 1);
        return aligned < ALIGNMENT ? ALIGNMENT : aligned;
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) {
            return true;
        }
        if (!(o instanceof Size)) {
            return false;
        }
        Size other = (Size) o;
        return width == other.width && height == other.height;
    }

    @Override
    public int hashCode() {
        return width * 31 + height;
    }

    @Override
    public String toString() {
        return width + "x" + height;
    }
}
