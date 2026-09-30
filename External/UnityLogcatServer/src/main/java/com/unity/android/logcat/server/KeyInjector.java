package com.unity.android.logcat.server;

import com.unity.android.logcat.server.wrappers.InputManagerWrapper;

import android.os.SystemClock;
import android.view.InputDevice;
import android.view.KeyCharacterMap;
import android.view.KeyEvent;

/**
 * Injects key events and typed text into the device.
 * <p>
 * There are two paths on purpose. Named keys - Back, Enter, the arrows - arrive as an
 * Android keycode and become a {@link KeyEvent} directly. Typed characters arrive as
 * text and are turned into key events by {@link KeyCharacterMap}, which is what makes
 * punctuation, shifted characters and non-US layouts work: the Editor sends the
 * character the user actually produced and lets the device work out which keystrokes
 * would have produced it, instead of the Editor trying to map every layout itself.
 */
public final class KeyInjector {
    public static final int ACTION_DOWN = 0;
    public static final int ACTION_UP = 1;

    private final InputManagerWrapper inputManager;
    private final int displayId;

    private KeyCharacterMap characterMap;

    public KeyInjector(InputManagerWrapper inputManager, int displayId) {
        this.inputManager = inputManager;
        this.displayId = displayId;
    }

    /**
     * @param action    ACTION_DOWN or ACTION_UP
     * @param keyCode   an Android {@code KeyEvent.KEYCODE_*} value
     * @param metaState Android {@code KeyEvent.META_*} flags
     */
    public void injectKey(int action, int keyCode, int metaState) {
        int keyAction;
        switch (action) {
            case ACTION_DOWN:
                keyAction = KeyEvent.ACTION_DOWN;
                break;
            case ACTION_UP:
                keyAction = KeyEvent.ACTION_UP;
                break;
            default:
                Logger.w("Ignoring unknown key action " + action);
                return;
        }

        long now = SystemClock.uptimeMillis();
        KeyEvent event = new KeyEvent(
            now, // downTime
            now, // eventTime
            keyAction,
            keyCode,
            0, // repeat
            metaState,
            KeyCharacterMap.VIRTUAL_KEYBOARD,
            0, // scanCode
            0, // flags
            InputDevice.SOURCE_KEYBOARD);

        inject(event);
    }

    /** Types {@code text} as if it had been entered on a keyboard. */
    public void injectText(String text) {
        if (text.isEmpty()) {
            return;
        }

        if (characterMap == null) {
            characterMap = KeyCharacterMap.load(KeyCharacterMap.VIRTUAL_KEYBOARD);
        }

        KeyEvent[] events = characterMap.getEvents(text.toCharArray());
        if (events == null) {
            // The virtual keyboard layout cannot produce one of these characters. There
            // is no keystroke sequence to fall back to, so say so and move on.
            Logger.w("Cannot type '" + text + "' with the virtual keyboard layout");
            return;
        }

        for (KeyEvent event : events) {
            inject(event);
        }
    }

    private void inject(KeyEvent event) {
        inputManager.inject(event, displayId);
    }
}
