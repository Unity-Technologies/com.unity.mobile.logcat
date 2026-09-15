# UnityLogcatServer

On-device server for the Android Logcat package's live screen streaming. It
mirrors a device display, encodes each frame as JPEG and writes the frames to a
socket that the Unity Editor reads.

This is not an Android application. It has no manifest, no resources and no
activity - it is a dexed jar started by `app_process`, running as the `shell`
user, which is what lets it call the hidden display-mirroring APIs that a normal
app cannot.

## Building

Requires a JDK 17+ and an Android SDK with `platforms/android-36` and
`build-tools/36.0.0`. A Unity installation with Android support ships both.

Point the build at the SDK with any one of:

* `local.properties` in this directory, containing `sdk.dir=<path>` (gitignored)
* the `ANDROID_HOME` environment variable
* the `ANDROID_SDK_ROOT` environment variable

Then:

```sh
./gradlew dexJar
```

which produces `build/outputs/unity-logcat-server.jar`.

The build uses the plain `java-library` plugin plus an explicit `d8` step rather
than the Android Gradle Plugin. AGP would produce an APK that then has to be
renamed, needs network access to the `google()` repository and pulls in a large
dependency tree - none of which this project has any use for.

Sources are compiled with `android.jar` replacing the JDK bootclasspath, so
reaching for a desktop-only API is a compile error rather than a crash on the
device.

## Running it by hand

`./gradlew runJar` pushes the jar and runs it in the foreground. Or, spelled out:

```sh
adb push build/outputs/unity-logcat-server.jar /data/local/tmp/
adb shell CLASSPATH=/data/local/tmp/unity-logcat-server.jar \
    app_process / com.unity.android.logcat.server.Server log_level=debug

# from another shell
adb forward tcp:27183 localabstract:unity_logcat_server
```

and then read frames from `127.0.0.1:27183`.

Options are `key=value` pairs; `Server.USAGE` lists them:

| Option | Default | Meaning |
| --- | --- | --- |
| `socket_name` | `unity_logcat_server` | abstract unix socket to listen on |
| `display_id` | `0` | display to capture |
| `max_size` | `1024` | longest side of the stream in pixels, 0 for native |
| `quality` | `70` | JPEG quality, 1..100 |
| `max_fps` | `30` | frame rate cap |
| `connect_timeout_ms` | `10000` | how long to wait for the Editor, 0 waits forever |
| `log_level` | `info` | `verbose`, `debug`, `info`, `warn`, `error` |

Log output goes to both logcat (tag `UnityLogcatServer`) and stderr, so the
Editor can surface a startup failure from the `adb shell` process it spawned.

## Lifecycle

The server handles exactly one client and then exits. There is no daemon, nothing
is left listening between sessions, and a second live-stream session is a second
process.

### Startup

1. The Editor pushes the jar to `/data/local/tmp/` and spawns
   `adb shell CLASSPATH=<jar> app_process / com.unity.android.logcat.server.Server <options>`.
   The process runs as the `shell` user, which is what makes the hidden
   display-mirroring APIs callable.
2. `Server.main` parses the options and sets the log level. Bad options exit
   immediately with the usage text.
3. `DisplayManagerGlobal` is resolved by reflection. If that class is missing the
   server fails here, before it has claimed anything.
4. A `LocalServerSocket` is opened on `socket_name` and `Listening on
   localabstract:<name>` is logged. Nothing is captured yet - the display is only
   mirrored once a client is actually there.
5. The Editor runs `adb forward tcp:<port> localabstract:<name>` and connects.
   `adb forward` only succeeds once the socket exists, so the Editor may have to
   retry: the server is spawned first, but there is no ordering guarantee between
   two separate adb invocations.
6. On `accept()`, the 12-byte stream header is written straight away. That header
   is what tells the Editor it has reached a real server of a protocol version it
   understands, rather than a forwarded port that merely happens to connect.
7. The capture session starts: a `HandlerThread`, an `ImageReader`, and a mirrored
   display pointed at the reader's surface. Frames flow from the capture thread;
   the main thread re-reads the display geometry every 500 ms and restarts the
   session if it changed.

### Shutdown

Every path ends in an explicit `System.exit`. `app_process` will not exit on its
own while a `Looper` or a non-daemon thread is alive, and the Editor is waiting
for its `adb shell` to return.

| Trigger | How it is noticed | Exit code |
| --- | --- | --- |
| Editor closes the connection, its process dies, or the forward is removed | the capture thread's write fails, or the disconnect-watch thread reads EOF | 0 |
| No client connects within `connect_timeout_ms` (default 10 s) | a watchdog thread exits the process out from under the blocked `accept()` | 1 |
| The captured display disappears | the geometry poll gets no `DisplayInfo` | 0 |
| Neither mirroring API works | `startSession` throws | 1 |
| Invalid options | `Options.parse` throws | 2 |
| Anything unexpected | caught in `main` and logged with a stack trace | 1 |

Three details matter for a clean stop:

* **The accept timeout exits the process rather than closing the socket.** Closing
  the server socket from the watchdog thread would look like the tidier option, but
  on Linux closing a file descriptor does not interrupt an `accept()` that another
  thread is already parked on - the server would stay wedged forever, which is the
  exact orphan the timeout exists to prevent. At that point nothing has been claimed
  that needs unwinding, so exiting is both simpler and the only thing that works.

* **The disconnect-watch thread exists because writes alone are not enough.** On a
  screen that has stopped changing, no frames are produced, so a departed client
  would go unnoticed and the server would sit there mirroring a display nobody is
  reading. The watch thread reads the socket and treats EOF as the end of the
  session. It is also where a control channel would be read from later.
* **The socket is closed before the streamer.** Closing it first unblocks a capture
  thread parked in a write, so teardown does not have to wait for it. Teardown then
  releases the mirrored display, closes the `ImageReader`, joins the capture thread
  (2 s cap) and recycles the reusable bitmaps.

### If the server is killed outright

Killing the `adb shell`, or the process on the device, skips all of the above and
leaks nothing that survives: the mirrored display and the `ImageReader` belong to
the process, and the abstract socket name disappears with it. Only the pushed jar
remains on disk, which is inert and overwritten by the next push.

A stale server from a previous session is therefore only a problem if it is still
*running* - it would own the socket name. Passing a per-session unique
`socket_name` avoids the collision entirely, and `connect_timeout_ms` bounds how
long an orphan can linger before it gives up on its own.

## Wire protocol

All integers big endian. See `Protocol.java`.

```
Stream header, once, 12 bytes:
  u32  magic            'U' 'L' 'S' '1' (0x554C5331)
  u32  protocolVersion  see serverProtocolVersion in gradle.properties
  u32  codec            1 = MJPEG

Frame, repeated, 20 byte header + payload:
  u64  ptsUs            microseconds since the first frame
  u32  width            pixels
  u32  height           pixels
  u32  payloadSize      bytes of encoded frame that follow
  u8[] payload          JPEG
```

Width and height travel with every frame instead of only in the stream header,
because they change when the device is rotated or the display is resized. The
Editor therefore never has to be told out of band that the geometry moved - it
just reads the next frame.

`serverProtocolVersion` lives in `gradle.properties` and is baked into the jar as
`BuildConfig.PROTOCOL_VERSION`, so that the Editor and the server cannot silently
drift apart. Bump it whenever the packet layout changes.

## Why JPEG

H.264 through `MediaCodec` would cost a fraction of the bandwidth, but the Editor
would then need a video decoder, and there is no H.264 decoder reachable from
Editor C#. A JPEG frame goes straight into `Texture2D.LoadImage`. Measured on a
Pixel 2 at `max_size=512 quality=70`: ~30 KB per frame, ~3.6 Mbps at 15 fps.

## Layout

| File | Role |
| --- | --- |
| `Server.java` | entry point, socket setup, client lifetime |
| `ScreenStreamer.java` | display mirroring, JPEG encoding, frame pacing |
| `Protocol.java` | wire format |
| `Options.java` | `key=value` command line |
| `DisplayInfo.java`, `Size.java` | value types |
| `Logger.java` | logging to logcat and stderr |
| `wrappers/DisplayManagerWrapper.java` | reflection over `DisplayManagerGlobal` |
| `wrappers/SurfaceControlWrapper.java` | reflection over `SurfaceControl` |

Two mirroring paths are attempted in order: `DisplayManagerGlobal
.createVirtualDisplay`, then `SurfaceControl.createDisplay`. Neither works
everywhere - `SurfaceControl.createDisplay` was removed in Android 15, and the
`DisplayManagerGlobal` overload is missing on some older versions (including
Android 10, where the `SurfaceControl` path is the one that runs) - so whichever
succeeds first wins.
