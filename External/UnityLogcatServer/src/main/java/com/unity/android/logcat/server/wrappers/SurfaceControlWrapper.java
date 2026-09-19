package com.unity.android.logcat.server.wrappers;

import android.annotation.SuppressLint;
import android.graphics.Rect;
import android.os.IBinder;
import android.view.Surface;

import java.lang.reflect.Method;

/**
 * Reflection over {@code android.view.SurfaceControl}.
 * <p>
 * This is the fallback path for creating a mirrored display. It predates
 * {@code DisplayManagerGlobal.createVirtualDisplay} and still works on devices
 * where that call is missing or broken, but {@code createDisplay} was removed in
 * Android 15, so it cannot be the primary path either. Whichever one works first
 * wins - see {@code ScreenStreamer.startSession}.
 */
@SuppressLint("PrivateApi")
public final class SurfaceControlWrapper {
    private static final Class<?> CLASS;

    static {
        try {
            CLASS = Class.forName("android.view.SurfaceControl");
        } catch (ClassNotFoundException e) {
            throw new AssertionError(e);
        }
    }

    private SurfaceControlWrapper() {
    }

    public static IBinder createDisplay(String name, boolean secure) throws ReflectiveOperationException {
        Method method = CLASS.getMethod("createDisplay", String.class, boolean.class);
        return (IBinder) method.invoke(null, name, secure);
    }

    public static void destroyDisplay(IBinder displayToken) throws ReflectiveOperationException {
        CLASS.getMethod("destroyDisplay", IBinder.class).invoke(null, displayToken);
    }

    public static void setDisplaySurface(IBinder displayToken, Surface surface) throws ReflectiveOperationException {
        CLASS.getMethod("setDisplaySurface", IBinder.class, Surface.class).invoke(null, displayToken, surface);
    }

    public static void setDisplayProjection(IBinder displayToken, int orientation, Rect layerStackRect, Rect displayRect)
            throws ReflectiveOperationException {
        CLASS.getMethod("setDisplayProjection", IBinder.class, int.class, Rect.class, Rect.class)
            .invoke(null, displayToken, orientation, layerStackRect, displayRect);
    }

    public static void setDisplayLayerStack(IBinder displayToken, int layerStack) throws ReflectiveOperationException {
        CLASS.getMethod("setDisplayLayerStack", IBinder.class, int.class).invoke(null, displayToken, layerStack);
    }

    public static void openTransaction() throws ReflectiveOperationException {
        CLASS.getMethod("openTransaction").invoke(null);
    }

    public static void closeTransaction() throws ReflectiveOperationException {
        CLASS.getMethod("closeTransaction").invoke(null);
    }
}
