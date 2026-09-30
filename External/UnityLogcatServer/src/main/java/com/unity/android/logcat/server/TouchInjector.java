package com.unity.android.logcat.server;

import com.unity.android.logcat.server.wrappers.InputManagerWrapper;

import android.os.SystemClock;
import android.view.InputDevice;
import android.view.MotionEvent;

import java.util.function.Supplier;

/**
 * Turns normalized touch positions from the Editor into {@link MotionEvent}s injected
 * into the device.
 * <p>
 * Positions arrive normalized rather than in pixels because the Editor's idea of the
 * screen size is always at least one frame stale, and can be a whole rotation stale.
 * Scaling here, against the display size the capture session is currently using, means
 * a touch always lands where the user pointed even if the display changed size in the
 * meantime.
 */
public final class TouchInjector {
    public static final int ACTION_DOWN = 0;
    public static final int ACTION_UP = 1;
    public static final int ACTION_MOVE = 2;
    public static final int ACTION_CANCEL = 3;

    private static final int MAX_POINTERS = 10;

    private final InputManagerWrapper inputManager;
    private final Supplier<Size> displaySize;
    private final int displayId;

    // One gesture per pointer. MotionEvent needs the time of the DOWN that started the
    // gesture on every later event, so it is remembered here rather than sent over the
    // wire: 0 means "this pointer is not down".
    private final long[] downTimes = new long[MAX_POINTERS];

    public TouchInjector(InputManagerWrapper inputManager, Supplier<Size> displaySize, int displayId) {
        this.inputManager = inputManager;
        this.displaySize = displaySize;
        this.displayId = displayId;
    }

    /**
     * @param action    one of the ACTION_* constants
     * @param pointerId which finger, 0 based
     * @param nx        horizontal position, 0..1 across the display
     * @param ny        vertical position, 0..1 down the display
     * @param pressure  0..1
     */
    public void inject(int action, int pointerId, float nx, float ny, float pressure) {
        if (pointerId < 0 || pointerId >= MAX_POINTERS) {
            Logger.w("Ignoring touch for pointer " + pointerId + ", only 0.." + (MAX_POINTERS - 1) + " are supported");
            return;
        }

        Size size = displaySize.get();
        if (size == null) {
            Logger.v("Ignoring touch, the display size is not known yet");
            return;
        }

        long now = SystemClock.uptimeMillis();
        int motionAction;

        switch (action) {
            case ACTION_DOWN:
                downTimes[pointerId] = now;
                motionAction = MotionEvent.ACTION_DOWN;
                break;
            case ACTION_MOVE:
                motionAction = MotionEvent.ACTION_MOVE;
                break;
            case ACTION_UP:
                motionAction = MotionEvent.ACTION_UP;
                break;
            case ACTION_CANCEL:
                motionAction = MotionEvent.ACTION_CANCEL;
                break;
            default:
                Logger.w("Ignoring unknown touch action " + action);
                return;
        }

        long downTime = downTimes[pointerId];
        if (downTime == 0) {
            // A move or an up with no down in front of it - the Editor and the device
            // disagree about the gesture, most likely because the stream restarted
            // mid-drag. Dropping it is better than injecting a malformed gesture.
            Logger.v("Ignoring touch action " + action + " for pointer " + pointerId + ", it is not down");
            return;
        }

        if (motionAction == MotionEvent.ACTION_UP || motionAction == MotionEvent.ACTION_CANCEL) {
            downTimes[pointerId] = 0;
        }

        MotionEvent.PointerProperties properties = new MotionEvent.PointerProperties();
        properties.id = pointerId;
        properties.toolType = MotionEvent.TOOL_TYPE_FINGER;

        MotionEvent.PointerCoords coords = new MotionEvent.PointerCoords();
        coords.x = size.pixelX(nx);
        coords.y = size.pixelY(ny);
        // A touchscreen event with zero pressure and size reads as a hover on some
        // devices, so an active pointer always reports some.
        coords.pressure = motionAction == MotionEvent.ACTION_UP
            ? 0f
            : Math.min(Math.max(pressure, 0.1f), 1f);
        coords.size = 1f;

        inputManager.inject(InputManagerWrapper.obtainMotionEvent(
            downTime, now, motionAction, properties, coords, InputDevice.SOURCE_TOUCHSCREEN), displayId);
    }
}
