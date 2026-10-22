package com.unity.android.logcat.server;

import com.unity.android.logcat.server.wrappers.InputManagerWrapper;

import android.os.SystemClock;
import android.view.InputDevice;
import android.view.KeyCharacterMap;
import android.view.KeyEvent;

import java.text.Normalizer;
import java.util.HashMap;
import java.util.Map;

/**
 * Injects key events and typed text into the device.
 * <p>
 * There are two paths on purpose. Named keys - Back, Enter, the arrows - arrive as an
 * Android keycode and become a {@link KeyEvent} directly. Typed characters arrive as
 * text and are turned into key events by {@link KeyCharacterMap}: the Editor sends the
 * character the user actually produced and lets the device work out which keystrokes
 * would have produced it, instead of the Editor trying to map every layout itself.
 * <p>
 * How far that reaches is the device's keyboard layout's decision. ASCII is typed
 * directly. An accented character is typed the way a keyboard with dead keys types it,
 * as the accent followed by the base letter, which works only for the accents that
 * layout has a dead key for - a Pixel's {@code Virtual.kcm} has five: grave, acute,
 * circumflex, tilde and diaeresis, so a caron or an ogonek cannot be typed at all.
 * Nor can anything outside the Latin script. Those characters are skipped and
 * reported, and the rest of the text still arrives; sending them would need the
 * device's clipboard rather than its keyboard.
 */
public final class KeyInjector {
    public static final int ACTION_DOWN = 0;
    public static final int ACTION_UP = 1;

    private final InputManagerWrapper inputManager;
    private final int displayId;

    private KeyCharacterMap characterMap;

    // When each held key went down. A KeyEvent carries that time on every later event
    // for the same key, so an app can tell how long the key was held from its up.
    private final Map<Integer, Long> downTimes = new HashMap<>();

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
        long downTime;
        if (keyAction == KeyEvent.ACTION_DOWN) {
            Long held = downTimes.get(keyCode);
            // A repeat belongs to the press that started it.
            downTime = held != null ? held : now;
            downTimes.put(keyCode, downTime);
        } else {
            Long held = downTimes.remove(keyCode);
            downTime = held != null ? held : now;
        }

        KeyEvent event = new KeyEvent(
            downTime,
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
        if (events != null) {
            for (KeyEvent event : events) {
                inject(event);
            }
            return;
        }

        // getEvents gives up on the whole array when a single character cannot be
        // typed, so the fallback goes character by character: one 'a' the keyboard has
        // never heard of no longer costs the rest of the message.
        StringBuilder skipped = null;
        for (char c : text.toCharArray()) {
            if (injectChar(c)) {
                continue;
            }
            if (skipped == null) {
                skipped = new StringBuilder();
            }
            skipped.append(c);
        }

        if (skipped != null) {
            Logger.w("Cannot type '" + skipped + "' with the virtual keyboard layout");
        }
    }

    /** @return false when there is no way to type this character. */
    private boolean injectChar(char c) {
        KeyEvent[] events = characterMap.getEvents(new char[] { c });
        if (events == null) {
            char[] composed = decompose(c);
            events = composed == null ? null : characterMap.getEvents(composed);
        }
        if (events == null) {
            return false;
        }

        for (KeyEvent event : events) {
            inject(event);
        }
        return true;
    }

    /**
     * The keystrokes that type an accented character on a keyboard with dead keys: the
     * accent, then the letter it belongs to.
     * <p>
     * Unicode already knows how every accented character is built, so the pair comes
     * from a canonical decomposition rather than from a table of our own. The accent
     * has to be the combining form, U+0301 and not U+00B4 - that is what a dead key on
     * the device's keyboard layout produces, while the spacing form matches no key at
     * all.
     *
     * @return null when the character is not an accented letter.
     */
    private static char[] decompose(char c) {
        String decomposed = Normalizer.normalize(String.valueOf(c), Normalizer.Form.NFD);
        if (decomposed.length() != 2) {
            return null;
        }

        char base = decomposed.charAt(0);
        char accent = decomposed.charAt(1);
        // Combining Diacritical Marks. Any other decomposition is not something a dead
        // key types.
        if (accent < '\u0300' || accent > '\u036F') {
            return null;
        }

        return new char[] { accent, base };
    }

    private void inject(KeyEvent event) {
        inputManager.inject(event, displayId);
    }
}
