package com.unity.android.logcat.server.wrappers;

import com.unity.android.logcat.server.Logger;

import android.annotation.SuppressLint;
import android.view.InputEvent;

import java.lang.reflect.Method;
import java.util.HashMap;
import java.util.Map;

/**
 * Reflection over the hidden input injection API.
 * <p>
 * Injecting an event into a window the caller does not own needs the
 * {@code INJECT_EVENTS} signature permission, which an app cannot hold but the
 * {@code shell} user can - so this works here and would not work from a normal app.
 */
@SuppressLint("PrivateApi")
public final class InputManagerWrapper {
    /** android.hardware.input.InputManager.INJECT_INPUT_EVENT_MODE_ASYNC */
    private static final int INJECT_INPUT_EVENT_MODE_ASYNC = 0;

    private final Object manager;
    private final Method injectInputEventMethod;

    private InputManagerWrapper(Object manager, Method injectInputEventMethod) {
        this.manager = manager;
        this.injectInputEventMethod = injectInputEventMethod;
    }

    public static InputManagerWrapper create() throws ReflectiveOperationException {
        Object manager;
        try {
            // Android 14 moved the singleton to InputManagerGlobal and removed
            // InputManager.getInstance().
            Class<?> globalClass = Class.forName("android.hardware.input.InputManagerGlobal");
            manager = globalClass.getDeclaredMethod("getInstance").invoke(null);
            Logger.d("Injecting input via InputManagerGlobal");
        } catch (ClassNotFoundException | NoSuchMethodException e) {
            Class<?> managerClass = Class.forName("android.hardware.input.InputManager");
            manager = managerClass.getDeclaredMethod("getInstance").invoke(null);
            Logger.d("Injecting input via InputManager");
        }

        if (manager == null) {
            throw new ReflectiveOperationException("Could not obtain an input manager instance");
        }

        Method method = manager.getClass().getMethod("injectInputEvent", InputEvent.class, int.class);
        return new InputManagerWrapper(manager, method);
    }

    /**
     * Cached per concrete event class, not once for all of them: {@code KeyEvent} and
     * {@code MotionEvent} each declare their own {@code setDisplayId}, so a method
     * resolved from one and invoked on the other throws
     * {@code IllegalArgumentException} - which is not a
     * {@code ReflectiveOperationException}, so it would escape the catch below, take
     * out the control reader thread and stop the stream with it.
     */
    private static final Map<Class<?>, Method> setDisplayIdMethods = new HashMap<>();
    private static boolean setDisplayIdUnavailable;

    /**
     * Targets an event at a specific display. Without this an event goes to the default
     * display, which is wrong when capturing any other one. The setter is hidden API, so
     * a device without it means input on secondary displays does not work - the video
     * stream is unaffected, hence a warning rather than a failure.
     */
    public static void setDisplayId(InputEvent event, int displayId) {
        if (setDisplayIdUnavailable) {
            return;
        }
        Class<?> eventClass = event.getClass();
        try {
            // Resolved on the concrete class: KeyEvent and MotionEvent each declare
            // their own, and which one exists on InputEvent varies by version.
            Method method = setDisplayIdMethods.get(eventClass);
            if (method == null) {
                method = eventClass.getMethod("setDisplayId", int.class);
                setDisplayIdMethods.put(eventClass, method);
            }
            method.invoke(event, displayId);
        } catch (ReflectiveOperationException | IllegalArgumentException e) {
            setDisplayIdUnavailable = true;
            Logger.w("setDisplayId is unavailable, input will go to the default display", e);
        }
    }

    /** @return false when the event was rejected, which the caller should not treat as fatal. */
    public boolean injectInputEvent(InputEvent event) {
        try {
            // Async: we do not wait for the event to be dispatched. A live view sends a
            // steady stream of moves and none of them is worth a round trip.
            Object result = injectInputEventMethod.invoke(manager, event, INJECT_INPUT_EVENT_MODE_ASYNC);
            return !(result instanceof Boolean) || (Boolean) result;
        } catch (ReflectiveOperationException e) {
            Logger.w("Failed to inject an input event", e);
            return false;
        }
    }
}
