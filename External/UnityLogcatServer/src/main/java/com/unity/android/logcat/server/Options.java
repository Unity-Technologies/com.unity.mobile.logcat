package com.unity.android.logcat.server;

import java.util.Locale;

/**
 * Command line options, passed as {@code key=value} pairs:
 *
 * <pre>
 * app_process / com.unity.android.logcat.server.Server display_id=0 max_size=1024
 * </pre>
 *
 * {@code key=value} rather than {@code --flags} because the whole command line is
 * handed to {@code adb shell}, which passes it through a shell on the device; the
 * fewer characters a shell wants to interpret, the better.
 */
public final class Options {
    private String socketName = BuildConfig.DEFAULT_SOCKET_NAME;
    private int displayId;
    private int maxSize = 1024;
    private int quality = 70;
    private int maxFps = 30;
    private int connectTimeoutMs = 10_000;
    private Logger.Level logLevel = Logger.Level.INFO;

    private Options() {
    }

    /** Abstract-namespace unix socket the server listens on. */
    public String getSocketName() {
        return socketName;
    }

    public int getDisplayId() {
        return displayId;
    }

    /** Longest side of the streamed image, in pixels. 0 means the display's own size. */
    public int getMaxSize() {
        return maxSize;
    }

    /** JPEG quality, 1..100. */
    public int getQuality() {
        return quality;
    }

    public int getMaxFps() {
        return maxFps;
    }

    /** How long to wait for the Editor to connect before giving up and exiting. */
    public int getConnectTimeoutMs() {
        return connectTimeoutMs;
    }

    public Logger.Level getLogLevel() {
        return logLevel;
    }

    public static Options parse(String... args) {
        Options options = new Options();

        for (String arg : args) {
            if (arg.isEmpty()) {
                continue;
            }

            int equals = arg.indexOf('=');
            if (equals == -1) {
                throw new IllegalArgumentException("Expected key=value, got '" + arg + "'");
            }
            String key = arg.substring(0, equals);
            String value = arg.substring(equals + 1);

            switch (key) {
                case "socket_name":
                    options.socketName = value;
                    break;
                case "display_id":
                    options.displayId = parseInt(key, value, 0, Integer.MAX_VALUE);
                    break;
                case "max_size":
                    options.maxSize = parseInt(key, value, 0, 16384);
                    break;
                case "quality":
                    options.quality = parseInt(key, value, 1, 100);
                    break;
                case "max_fps":
                    options.maxFps = parseInt(key, value, 1, 240);
                    break;
                case "connect_timeout_ms":
                    options.connectTimeoutMs = parseInt(key, value, 0, 600_000);
                    break;
                case "log_level":
                    options.logLevel = parseLogLevel(value);
                    break;
                default:
                    throw new IllegalArgumentException("Unknown option '" + key + "'");
            }
        }

        return options;
    }

    private static int parseInt(String key, String value, int min, int max) {
        int parsed;
        try {
            parsed = Integer.parseInt(value);
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("Option '" + key + "' is not a number: '" + value + "'");
        }
        if (parsed < min || parsed > max) {
            throw new IllegalArgumentException("Option '" + key + "' must be in [" + min + ".." + max + "], got " + parsed);
        }
        return parsed;
    }

    private static Logger.Level parseLogLevel(String value) {
        try {
            return Logger.Level.valueOf(value.toUpperCase(Locale.ROOT));
        } catch (IllegalArgumentException e) {
            throw new IllegalArgumentException("Unknown log_level '" + value + "'");
        }
    }

    @Override
    public String toString() {
        return "socket_name=" + socketName
            + " display_id=" + displayId
            + " max_size=" + maxSize
            + " quality=" + quality
            + " max_fps=" + maxFps
            + " log_level=" + logLevel;
    }
}
