package com.unity.android.logcat.server.wrappers;

import com.unity.android.logcat.server.DisplayInfo;
import com.unity.android.logcat.server.Logger;
import com.unity.android.logcat.server.Size;

import android.annotation.SuppressLint;
import android.hardware.display.VirtualDisplay;
import android.view.Surface;

import java.lang.reflect.Method;

/**
 * Reflection over {@code android.hardware.display.DisplayManagerGlobal}.
 * <p>
 * The public {@code DisplayManager} API cannot mirror a display into a surface
 * without the {@code CAPTURE_VIDEO_OUTPUT} permission, which a shell process
 * cannot hold. The hidden API can, and the server runs as {@code shell} via
 * app_process, so it is allowed to call it.
 */
@SuppressLint("PrivateApi")
public final class DisplayManagerWrapper {
    private final Object manager; // android.hardware.display.DisplayManagerGlobal
    private Method getDisplayInfoMethod;
    private Method createVirtualDisplayMethod;

    private DisplayManagerWrapper(Object manager) {
        this.manager = manager;
    }

    public static DisplayManagerWrapper create() throws ReflectiveOperationException {
        Class<?> clazz = Class.forName("android.hardware.display.DisplayManagerGlobal");
        Object instance = clazz.getDeclaredMethod("getInstance").invoke(null);
        return new DisplayManagerWrapper(instance);
    }

    /** @return null when the display does not exist. */
    public DisplayInfo getDisplayInfo(int displayId) throws ReflectiveOperationException {
        if (getDisplayInfoMethod == null) {
            getDisplayInfoMethod = manager.getClass().getMethod("getDisplayInfo", int.class);
        }
        Object displayInfo = getDisplayInfoMethod.invoke(manager, displayId);
        if (displayInfo == null) {
            return null;
        }

        Class<?> cls = displayInfo.getClass();
        // logicalWidth/logicalHeight already account for the current rotation.
        int width = cls.getDeclaredField("logicalWidth").getInt(displayInfo);
        int height = cls.getDeclaredField("logicalHeight").getInt(displayInfo);
        int rotation = cls.getDeclaredField("rotation").getInt(displayInfo);
        int layerStack = cls.getDeclaredField("layerStack").getInt(displayInfo);
        int flags = cls.getDeclaredField("flags").getInt(displayInfo);
        int dpi = cls.getDeclaredField("logicalDensityDpi").getInt(displayInfo);

        return new DisplayInfo(displayId, new Size(width, height), rotation, layerStack, flags, dpi);
    }

    public int[] getDisplayIds() {
        try {
            return (int[]) manager.getClass().getMethod("getDisplayIds").invoke(manager);
        } catch (ReflectiveOperationException e) {
            Logger.w("Could not list display ids", e);
            return new int[] { 0 };
        }
    }

    /**
     * Creates a virtual display mirroring {@code displayIdToMirror} into
     * {@code surface}, via the hidden static
     * {@code DisplayManager.createVirtualDisplay(String, int, int, int, Surface)}.
     */
    public VirtualDisplay createVirtualDisplay(String name, int width, int height, int displayIdToMirror, Surface surface)
            throws ReflectiveOperationException {
        if (createVirtualDisplayMethod == null) {
            createVirtualDisplayMethod = android.hardware.display.DisplayManager.class
                .getMethod("createVirtualDisplay", String.class, int.class, int.class, int.class, Surface.class);
        }
        return (VirtualDisplay) createVirtualDisplayMethod.invoke(null, name, width, height, displayIdToMirror, surface);
    }
}
