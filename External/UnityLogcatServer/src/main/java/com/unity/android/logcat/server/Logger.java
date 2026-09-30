package com.unity.android.logcat.server;

import android.util.Log;

/**
 * Logging.
 * <p>
 * Everything is written both to logcat and to stderr. stderr is what the Editor
 * sees on the {@code adb shell} process it spawned, so it is the channel the
 * Editor surfaces to the user when the server fails to start; logcat keeps a copy
 * for after-the-fact diagnosis.
 */
public final class Logger {
    public enum Level {
        VERBOSE, DEBUG, INFO, WARN, ERROR
    }

    private static final String TAG = "UnityLogcatServer";
    private static final String PREFIX = "[unity-logcat-server] ";

    private static Level threshold = Level.INFO;

    private Logger() {
    }

    public static void setLevel(Level level) {
        threshold = level;
    }

    public static boolean isEnabled(Level level) {
        return level.ordinal() >= threshold.ordinal();
    }

    public static void v(String message) {
        log(Level.VERBOSE, message, null);
    }

    public static void d(String message) {
        log(Level.DEBUG, message, null);
    }

    public static void i(String message) {
        log(Level.INFO, message, null);
    }

    public static void w(String message) {
        log(Level.WARN, message, null);
    }

    public static void w(String message, Throwable throwable) {
        log(Level.WARN, message, throwable);
    }

    public static void e(String message) {
        log(Level.ERROR, message, null);
    }

    public static void e(String message, Throwable throwable) {
        log(Level.ERROR, message, throwable);
    }

    private static void log(Level level, String message, Throwable throwable) {
        if (!isEnabled(level)) {
            return;
        }

        switch (level) {
            case VERBOSE:
                Log.v(TAG, message, throwable);
                break;
            case DEBUG:
                Log.d(TAG, message, throwable);
                break;
            case INFO:
                Log.i(TAG, message, throwable);
                break;
            case WARN:
                Log.w(TAG, message, throwable);
                break;
            case ERROR:
                Log.e(TAG, message, throwable);
                break;
            default:
                break;
        }

        java.io.PrintStream stream = level.ordinal() >= Level.WARN.ordinal() ? System.err : System.out;
        stream.println(PREFIX + level + ": " + message);
        if (throwable != null) {
            throwable.printStackTrace(stream);
        }
        stream.flush();
    }
}
