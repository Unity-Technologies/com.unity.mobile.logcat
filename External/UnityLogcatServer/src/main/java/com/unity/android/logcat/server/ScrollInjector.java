package com.unity.android.logcat.server;

import com.unity.android.logcat.server.wrappers.InputManagerWrapper;

import android.os.SystemClock;
import android.view.InputDevice;
import android.view.MotionEvent;

import java.util.function.Supplier;

/**
 * Turns scroll wheel movement from the Editor into {@code ACTION_SCROLL}
 * {@link MotionEvent}s injected into the device.
 * <p>
 * Unlike a touch this is not part of a gesture, so there is no down time to remember
 * and no pointer to keep track of - each scroll stands alone. The magnitude travels in
 * the {@code VSCROLL} and {@code HSCROLL} axes rather than in the position, which is
 * why this needs the {@code PointerCoords} form of {@code MotionEvent.obtain}; the
 * position still matters, because a scroll goes to whatever view is under the pointer.
 * <p>
 * The event claims to come from a mouse: a touchscreen has no scroll axis, so an event
 * from {@code SOURCE_TOUCHSCREEN} carrying one would be dropped.
 */
public final class ScrollInjector {
    private final InputManagerWrapper inputManager;
    private final Supplier<Size> displaySize;
    private final int displayId;

    public ScrollInjector(InputManagerWrapper inputManager, Supplier<Size> displaySize, int displayId) {
        this.inputManager = inputManager;
        this.displaySize = displaySize;
        this.displayId = displayId;
    }

    /**
     * @param nx      horizontal position, 0..1 across the display
     * @param ny      vertical position, 0..1 down the display
     * @param hScroll notches to the right, negative for left
     * @param vScroll notches away from the user, negative for towards
     */
    public void inject(float nx, float ny, float hScroll, float vScroll) {
        if (hScroll == 0f && vScroll == 0f) {
            return;
        }

        Size size = displaySize.get();
        if (size == null) {
            Logger.v("Ignoring scroll, the display size is not known yet");
            return;
        }

        long now = SystemClock.uptimeMillis();

        // Clamped rather than rejected, as for touch: the Editor sends where the mouse
        // is, and the edge of the view should read as the edge of the screen.
        float x = clamp01(nx) * size.getWidth();
        float y = clamp01(ny) * size.getHeight();

        MotionEvent.PointerProperties[] properties = new MotionEvent.PointerProperties[1];
        properties[0] = new MotionEvent.PointerProperties();
        properties[0].id = 0;
        properties[0].toolType = MotionEvent.TOOL_TYPE_MOUSE;

        MotionEvent.PointerCoords[] coords = new MotionEvent.PointerCoords[1];
        coords[0] = new MotionEvent.PointerCoords();
        coords[0].x = x;
        coords[0].y = y;
        coords[0].setAxisValue(MotionEvent.AXIS_VSCROLL, vScroll);
        coords[0].setAxisValue(MotionEvent.AXIS_HSCROLL, hScroll);

        // A mouse has to be hovering over a view before a scroll means anything to it,
        // and nothing else moves this pointer: the Editor sends a position with every
        // scroll, not a stream of moves. So the hover is sent first, every time.
        inject(MotionEvent.ACTION_HOVER_MOVE, now, properties, coords);
        inject(MotionEvent.ACTION_SCROLL, now, properties, coords);
    }

    private void inject(int action, long now, MotionEvent.PointerProperties[] properties,
            MotionEvent.PointerCoords[] coords) {
        MotionEvent event = MotionEvent.obtain(
            now, // downTime - a scroll has no gesture behind it, so it is its own
            now,
            action,
            1, // pointerCount
            properties,
            coords,
            0, // metaState
            0, // buttonState
            1f, // xPrecision
            1f, // yPrecision
            0, // deviceId
            0, // edgeFlags
            InputDevice.SOURCE_MOUSE,
            0); // flags

        try {
            if (displayId != 0) {
                InputManagerWrapper.setDisplayId(event, displayId);
            }
            if (!inputManager.injectInputEvent(event)) {
                Logger.d("Scroll event " + action + " was rejected");
            }
        } finally {
            event.recycle();
        }
    }

    private static float clamp01(float value) {
        if (value < 0f) {
            return 0f;
        }
        return value > 1f ? 1f : value;
    }
}
