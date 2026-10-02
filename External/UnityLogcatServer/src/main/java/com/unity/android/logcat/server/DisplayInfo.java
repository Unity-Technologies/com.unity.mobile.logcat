package com.unity.android.logcat.server;

/** Snapshot of a logical display, read out of the hidden {@code DisplayInfo} class. */
public final class DisplayInfo {
    private final int displayId;
    private final Size size;
    private final int rotation;
    private final int layerStack;
    private final int flags;
    private final int dpi;

    public DisplayInfo(int displayId, Size size, int rotation, int layerStack, int flags, int dpi) {
        this.displayId = displayId;
        this.size = size;
        this.rotation = rotation;
        this.layerStack = layerStack;
        this.flags = flags;
        this.dpi = dpi;
    }

    public int getDisplayId() {
        return displayId;
    }

    /** Logical size, already rotated: it swaps when the device is turned. */
    public Size getSize() {
        return size;
    }

    public int getRotation() {
        return rotation;
    }

    public int getLayerStack() {
        return layerStack;
    }

    public int getFlags() {
        return flags;
    }

    public int getDpi() {
        return dpi;
    }

    @Override
    public String toString() {
        return "display " + displayId + " " + size + " rotation=" + rotation + " dpi=" + dpi;
    }
}
