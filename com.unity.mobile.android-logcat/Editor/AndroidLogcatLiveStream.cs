using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Live view of an Android device screen.
    /// <para>
    /// A small server jar (built from External~/UnityLogcatServer, shipped in the
    /// package as External~/unity-logcat-server.jar) is pushed to the device and run
    /// by app_process as the shell user. It mirrors a display, encodes each frame as
    /// JPEG and writes the frames to an abstract unix socket, which adb forwards to a
    /// local TCP port that we read here.
    /// </para>
    /// <para>
    /// The transport is a socket rather than the server's stdout because it is
    /// bidirectional - the same connection can later carry input events to the
    /// device - and it keeps frame data off a stream that also carries log output.
    /// </para>
    /// <para>
    /// Threading: a reader thread does the blocking socket reads and hands the newest
    /// frame over; <see cref="Update"/> turns it into a texture on the main thread,
    /// because Texture2D can only be touched there.
    /// </para>
    /// </summary>
    internal class AndroidLogcatLiveStream
    {
        internal enum Result
        {
            Success,
            Failure
        }

        internal enum FailureType
        {
            None,
            JarNotFound
        }

        /// <summary>
        /// The type byte that starts every Editor to server control message. Values match
        /// the TYPE_* constants in ControlReader.java, and the server stops reading
        /// control input on one it does not recognize, since it would no longer know
        /// where the next message begins.
        /// </summary>
        internal enum ControlMessage : byte
        {
            Touch = 1,
            Key = 2,
            Text = 3,
            Scroll = 4
        }

        /// <summary>Values match the action byte in ControlReader.java.</summary>
        internal enum TouchAction : byte
        {
            Down = 0,
            Up = 1,
            Move = 2,
            /// <summary>Abandons the gesture without a tap, e.g. the mouse left the window.</summary>
            Cancel = 3
        }

        /// <summary>Values match the action byte in ControlReader.java.</summary>
        internal enum KeyAction : byte
        {
            Down = 0,
            Up = 1
        }

        static string m_ServerJarPath;

        static string GetServerJarPath()
        {
            if (!string.IsNullOrEmpty(m_ServerJarPath))
                return m_ServerJarPath;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AndroidLogcatLiveStream).Assembly);
            if (package == null)
                throw new FileNotFoundException($"Couldn't locate the Android Logcat package to find {kServerJarName} in it.");

            m_ServerJarPath = Path.Combine(package.resolvedPath, kServerExternalFolder, kServerJarName);
            return m_ServerJarPath;
        }

        // Android KeyEvent.META_* flags.
        const int kMetaShiftOn = 0x1;
        const int kMetaAltOn = 0x2;
        const int kMetaCtrlOn = 0x1000;

        // Must stay in step with External/UnityLogcatServer: Protocol.java and the
        // serverProtocolVersion / serverSocketName / serverDevicePath entries in
        // gradle.properties. The server sends its version in the stream header, so a
        // mismatch is reported rather than misparsed.
        const uint kProtocolMagic = 0x554C5331; // "ULS1"
        const int kProtocolVersion = 5;
        const int kCodecMjpeg = 1;
        const int kStreamHeaderSize = 20; // magic + version + codec + flags + serverPid
        const int kFrameHeaderSize = 20;  // ptsUs + width + height + payloadSize

        // Protocol.FLAG_CONTROL_SUPPORTED: the server was able to set up input
        // injection, so touch messages will actually do something.
        const int kFlagControlSupported = 1;

        // Largest fixed-size message: the key one, at type + action + keyCode + metaState.
        const int kControlMessageSize = 10;
        const int kMaxTextBytes = 4096;
        // Positions go over the wire normalized, so the server can scale them against
        // the display size it is currently capturing rather than trusting ours, which
        // is always at least a frame - and possibly a whole rotation - out of date.
        const float kNormalizedMax = 65535.0f;
        // Scroll notches go over as fixed point, so a trackpad's fractions survive
        // without putting a float on the wire. Must match SCROLL_SCALE in ControlReader.
        const float kScrollScale = 256.0f;
        // Unity reports roughly three lines per wheel notch, where Android counts
        // notches, so the delta is divided by this on the way out.
        const float kUnityScrollLinesPerNotch = 3.0f;

        const string kServerJarName = "unity-logcat-server.jar";
        // The jar is pushed under a name of its own per session - see StartStreaming.
        const string kServerDeviceFolder = "/data/local/tmp";
        const string kServerDeviceNamePrefix = "unity-logcat-server";
        const string kServerMainClass = "com.unity.android.logcat.server.Server";
        const string kServerExternalFolder = "External~";

        // Stream settings live in AndroidLogcatSettings, under Preferences. The
        // arguments of StartStreaming override them, which is what the tests use.

        // How long the server waits for us, and how long we spend trying to reach it.
        // The server's own timeout is the longer of the two, so that it is always us
        // who gives up first and the server is never left listening for a client that
        // has already stopped trying.
        const int kConnectTimeoutMs = 10000;
        const int kServerConnectTimeoutMs = 15000;
        const int kConnectRetryDelayMs = 100;

        // A frame is a JPEG of a phone screen; anything this large means the stream
        // has desynchronized and we should fail instead of allocating wildly.
        const int kMaxFrameSize = 32 * 1024 * 1024;

        // The info column beside the image. Capped to a fraction of the available width
        // as well, so a narrow window does not lose the image entirely to it.
        const float kStatsWidth = 190;
        const float kStatsMargin = 8;
        const float kNavigationSpacing = 6;
        const float kNavigationButtonWidth = 60;
        // A row each: both labels are too wide for the two of them to share the stats
        // column without being clipped.
        const float kDebugButtonWidth = 150;

        static class Styles
        {
            internal static readonly GUIContent StreamSize = new GUIContent("Stream size",
                "Size of the streamed image, which is the device display scaled down to fit max_size.");
            internal static readonly GUIContent FrameRate = new GUIContent("Frame rate",
                "Frames arriving per second. A mirrored display only produces a frame when the screen changes, so an idle device sends almost none.");
            internal static readonly GUIContent Bandwidth = new GUIContent("Bandwidth",
                "Megabits per second arriving over adb.");
            internal static readonly GUIContent Input = new GUIContent("Input",
                "Click or drag the image to send touch events to the device, scroll the wheel over it to " +
                "scroll on the device, and click it then type to send keys. Select all, copy and paste go " +
                "to the device and use its clipboard; other Ctrl and Cmd combinations stay in the Editor.");

            // Same glyphs and wording as the navigation row in the Inputs window.
            internal static readonly GUIContent Back = new GUIContent("◄",
                "Send Back key event. The Escape key does the same once the image has focus.");
            internal static readonly GUIContent Home = new GUIContent("●", "Send Home key event");
            internal static readonly GUIContent Recents = new GUIContent("■", "Send Overview key event");

            internal static readonly GUIContent DeveloperMode = new GUIContent("Developer Mode");
            internal static readonly GUIContent Socket = new GUIContent("Socket",
                "Abstract unix socket the on-device server is listening on.");
            internal static readonly GUIContent ForwardedPort = new GUIContent("Port",
                "Local TCP port adb forwards to that socket.");
            internal static readonly GUIContent ServerOnDevice = new GUIContent("Server",
                "Where the server jar was pushed on the device.");
            internal static readonly GUIContent ServerPid = new GUIContent("Server pid",
                "Process id of the server on the device, for adb shell kill or ps.");
            internal static readonly GUIContent RebuildJar = new GUIContent("Rebuild server",
                "Run 'gradlew dexJar' on External/UnityLogcatServer, which also copies the jar into the " +
                "package, then restart the stream so the device picks the new one up and point the " +
                "Logcat window at the server that comes back. Only available in the package's own " +
                "repository, where that Gradle project sits next to the package.");
            internal static readonly GUIContent ShowServerLogcat = new GUIContent("Show server logs",
                "Open the Android Logcat window filtered to this server's process.");
        }

        AndroidLogcatRuntimeBase m_Runtime;
        IAndroidLogcatDevice m_Device;
        Action<Result> m_OnStopLiveStream;
        FailureType m_FailureType;

        Process m_ServerProcess;
        readonly StringBuilder m_ServerLog = new StringBuilder();
        readonly StringBuilder m_Errors = new StringBuilder();
        string m_SocketName;
        // Where this session's jar lives on the device, unique per session - see
        // StartStreaming for why it cannot be a shared path.
        string m_ServerDevicePath;
        int m_ForwardedPort = -1;

        Thread m_ReaderThread;
        volatile bool m_Stop;
        volatile string m_ReaderError;
        volatile bool m_StreamEnded;

        // Guards the connection so that StopStreaming can close it from the main
        // thread while the reader thread is blocked in a read on it.
        readonly object m_ConnectionLock = new object();
        TcpClient m_Client;
        NetworkStream m_Stream;

        // Frame handover, reader thread -> main thread.
        readonly object m_FrameLock = new object();
        byte[] m_PendingFrame;
        int m_PendingFrameSize;
        int m_PendingWidth;
        int m_PendingHeight;
        long m_ReceivedBytes;
        int m_ReceivedFrames;

        // Frame buffers are reused rather than allocated per frame, which at 30 fps was
        // a few MB per second of short-lived garbage.
        //
        // A buffer is owned by exactly one of four places at any moment: this free list,
        // the reader thread filling it, the pending slot, or the main thread decoding it.
        // It only ever moves between them under m_FrameLock, and the main thread is what
        // hands it back, so the reader cannot overwrite a buffer being decoded. Three is
        // the most that can be in flight at once - one being filled, one pending, one
        // being decoded.
        const int kMaxFrameBuffers = 3;
        readonly Stack<byte[]> m_FreeFrameBuffers = new Stack<byte[]>(kMaxFrameBuffers);

        volatile bool m_ControlSupported;
        // Reported by the server in the stream header, so it is exact rather than
        // guessed from the process table, where several app_process entries can exist.
        volatile int m_ServerPid;
        // Set by the Rebuild jar button, cleared once the Logcat window has been
        // pointed at the server that came up. The pid is not known when the stream is
        // started - it arrives in the stream header, on the reader thread - so this
        // waits for it rather than guessing.
        bool m_ShowLogcatWhenServerStarts;
        readonly byte[] m_ControlMessage = new byte[kControlMessageSize];
        bool m_TouchDown;
        bool m_ControlWriteFailed;

        Texture2D m_Texture;
        int m_FrameWidth;
        int m_FrameHeight;
        double m_Fps;
        double m_Mbps;
        DateTime m_StatsTime;
        long m_StatsBytes;
        int m_StatsFrames;

        internal bool IsStreaming => m_ReaderThread != null;
        internal string Errors => m_Errors.ToString();
        internal Texture2D Texture => m_Texture;

        /// <summary>
        /// Whether the server can inject input. False means the device refused to set it
        /// up, in which case the view is read only and says so - better than accepting
        /// clicks that quietly go nowhere.
        /// </summary>
        internal bool ControlSupported => m_ControlSupported;

        /// <summary>
        /// Input is always on when the device supports it. There is no toggle: sending an
        /// event costs a handful of bytes and nothing at all when idle, so the only
        /// argument for one would be avoiding stray input, and a window does not click or
        /// type by itself.
        /// </summary>
        bool CanSendInput => IsStreaming && m_ControlSupported;

        /// <summary>Frames read off the socket since streaming started.</summary>
        internal int FramesReceived
        {
            get
            {
                lock (m_FrameLock)
                    return m_ReceivedFrames;
            }
        }

        internal AndroidLogcatLiveStream(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            m_Runtime.Update += Update;
            m_Runtime.Closing += Cleanup;
        }

        void Cleanup()
        {
            if (m_Runtime == null)
                return;
            if (IsStreaming)
                Shutdown(Result.Success);
            DestroyTexture();
            m_Runtime = null;
        }

        internal void StartStreaming(IAndroidLogcatDevice device,
            Action<Result> onStopLiveStream,
            int? maxSize = null,
            int? quality = null,
            int? maxFps = null,
            string displayId = null)
        {
            if (device == null)
                throw new InvalidOperationException("No device selected");
            if (IsStreaming)
                throw new InvalidOperationException("Already streaming");

            m_Errors.Clear();
            m_FailureType = FailureType.None;
            lock (m_ServerLog)
                m_ServerLog.Clear();
            DestroyTexture();

            m_Device = device;
            m_OnStopLiveStream = onStopLiveStream;
            m_Stop = false;
            m_ReaderError = null;
            m_StreamEnded = false;
            m_ControlSupported = false;
            m_ServerPid = 0;
            m_ControlWriteFailed = false;
            m_TouchDown = false;
            m_FrameWidth = 0;
            m_FrameHeight = 0;
            m_Fps = 0;
            m_Mbps = 0;
            m_StatsTime = DateTime.Now;
            m_StatsBytes = 0;
            m_StatsFrames = 0;
            lock (m_FrameLock)
            {
                m_PendingFrame = null;
                m_PendingFrameSize = 0;
                m_ReceivedBytes = 0;
                m_ReceivedFrames = 0;
            }

            try
            {
                // One id for the session, used for both the socket and the jar, and
                // settled before anything is pushed - the push needs the path.
                //
                // The socket name has to be unique so that a server left over from a
                // previous run cannot own the name we are about to listen on. The jar
                // path has to be unique because `adb push` rewrites its destination in
                // place rather than replacing it: with a shared name, starting a stream
                // while the previous server is still on its way out would truncate the
                // file that one is executing from, and a class it had not loaded yet
                // would fail to load.
                var sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
                m_SocketName = "unity_logcat_server_" + sessionId;
                m_ServerDevicePath = $"{kServerDeviceFolder}/{kServerDeviceNamePrefix}-{sessionId}.jar";

                // Before anything else, because a dark screen produces no frames at all
                // and the wait for the first one would just time out.
                device.WakeUp();

                // Before pushing ours, so it cannot sweep away what it is about to push.
                RemoveStaleServerJars(device);

                var jarPath = GetServerJarPath();
                if (!File.Exists(jarPath))
                {
                    m_FailureType = FailureType.JarNotFound;
                    var error = $"{kServerJarName} is missing from the package, live streaming is unavailable.\n" +
    $"Expected it at {jarPath}\n" +
    "Build it by running 'gradlew dexJar' in External/UnityLogcatServer.";
                    AppendError(error);
                    Shutdown(Result.Failure);
                    return;
                }

                PushServer(device, jarPath);

                var settings = m_Runtime.Settings;
                StartServerProcess(device,
                    maxSize ?? settings.LiveStreamMaxSize,
                    quality ?? settings.LiveStreamQuality,
                    maxFps ?? settings.LiveStreamMaxFps,
                    displayId);
                m_ForwardedPort = SetupPortForward(device, m_SocketName);

                // Connecting is retried until the server has created its socket, so it
                // happens on the reader thread rather than stalling the main thread.
                m_ReaderThread = new Thread(ReadFrames)
                {
                    Name = "AndroidLogcatLiveStream",
                    IsBackground = true
                };
                m_ReaderThread.Start();
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.ToString());
                AppendError(ex.Message);
                // Nothing is streaming, so unwind whatever did get set up and report
                // through the callback instead of throwing into OnGUI.
                Shutdown(Result.Failure);
            }
        }

        internal bool StopStreaming()
        {
            if (!IsStreaming)
                return false;
            Shutdown(Result.Success);
            return m_Errors.Length == 0;
        }

        /// <summary>
        /// Tears down everything StartStreaming may have set up, in the reverse order,
        /// and reports the outcome. Safe to call when only part of the setup happened.
        /// </summary>
        void Shutdown(Result result)
        {
            m_Stop = true;
            // Whatever was waiting for a pid is not getting one now.
            m_ShowLogcatWhenServerStarts = false;

            // The connection goes first: closing it is what unblocks a reader thread
            // parked in a read, so the join below does not have to wait it out.
            CloseConnection();

            var thread = m_ReaderThread;
            m_ReaderThread = null;
            if (thread != null && !thread.Join(TimeSpan.FromSeconds(2)))
                AndroidLogcatInternalLog.Log("Live stream reader thread did not stop in time");

            // With the reader gone, and this being the main thread, nothing can still be
            // holding a buffer - and once the stream is over they are only memory. A
            // frame off a big display is worth a couple of MB.
            lock (m_FrameLock)
            {
                m_PendingFrame = null;
                m_PendingFrameSize = 0;
                m_FreeFrameBuffers.Clear();
            }

            KillServerProcess();
            RemovePortForward();
            RemoveServerJar();

            if (result == Result.Failure)
                AppendServerLog();

            m_Device = null;
            var callback = m_OnStopLiveStream;
            m_OnStopLiveStream = null;
            callback?.Invoke(result);
        }

        void Update()
        {
            if (!IsStreaming)
                return;

            // The header has landed, so the new server can be named. Done here rather
            // than where the pid is parsed, because that is the reader thread and this
            // opens an EditorWindow.
            if (m_ShowLogcatWhenServerStarts && m_ServerPid > 0)
            {
                m_ShowLogcatWhenServerStarts = false;
                ShowServerLogcat();
            }

            ApplyPendingFrame();

            var error = m_ReaderError;
            if (!string.IsNullOrEmpty(error))
            {
                AppendError(error);
                Shutdown(Result.Failure);
                return;
            }

            if (m_StreamEnded)
            {
                // A reader that ended without recording an error, which today can only
                // mean Stop was asked for - the loop has no other way out.
                //
                // The connection closing on its own does not arrive here: reading hits
                // end of stream, which is an exception, so it goes through the branch
                // above. That is deliberate. A stream that ends without the user asking
                // is worth reporting, and reporting it as a failure is what appends the
                // server's own log, which is where the reason lives - the captured
                // display went away, the process was killed, and so on.
                Shutdown(Result.Success);
            }
        }

        void ApplyPendingFrame()
        {
            byte[] frame;
            int size;
            int width, height;
            long bytes;
            int frames;

            lock (m_FrameLock)
            {
                frame = m_PendingFrame;
                size = m_PendingFrameSize;
                m_PendingFrame = null;
                m_PendingFrameSize = 0;
                width = m_PendingWidth;
                height = m_PendingHeight;
                bytes = m_ReceivedBytes;
                frames = m_ReceivedFrames;
            }

            if (frame != null)
            {
                if (m_Texture == null)
                    m_Texture = new Texture2D(2, 2);
                // LoadImage resizes the texture to the incoming frame, which is how a
                // rotation is absorbed: the server just starts sending a new size.
                //
                // Decoded through a span rather than the byte[] overload, which would
                // take the whole buffer: a reused buffer is usually larger than the
                // frame sitting in it.
                if (ImageConversion.LoadImage(m_Texture, new ReadOnlySpan<byte>(frame, 0, size)))
                {
                    m_FrameWidth = width;
                    m_FrameHeight = height;
                }

                // Returned whether or not it decoded - a frame this thread could not
                // read is still a buffer the reader can fill.
                ReturnFrameBuffer(frame);
            }

            var now = DateTime.Now;
            var elapsed = (now - m_StatsTime).TotalSeconds;
            if (elapsed >= 1.0)
            {
                m_Fps = (frames - m_StatsFrames) / elapsed;
                m_Mbps = (bytes - m_StatsBytes) * 8 / elapsed / 1000000.0;
                m_StatsTime = now;
                m_StatsFrames = frames;
                m_StatsBytes = bytes;
            }
        }

        // ------------------------------------------------------------------
        // Server setup
        // ------------------------------------------------------------------

        void PushServer(IAndroidLogcatDevice device, string jarPath)
        {
            AndroidLogcatInternalLog.Log($"Pushing {jarPath} to {m_ServerDevicePath}");
            // Pushed on every start: the destination name is new each time, so there is
            // never a stale jar to reuse and never one in use to overwrite.
            m_Runtime.Tools.ADB.Run(new[]
            {
                $"-s {device.Id}",
                "push",
                $"\"{jarPath}\"",
                m_ServerDevicePath
            }, $"Failed to push {kServerJarName} to the device");
        }

        void StartServerProcess(IAndroidLogcatDevice device, int maxSize, int quality, int maxFps, string displayId)
        {
            var args = new StringBuilder();
            args.Append($"-s {device.Id} shell CLASSPATH={m_ServerDevicePath} app_process / {kServerMainClass}");
            args.Append($" socket_name={m_SocketName}");
            args.Append($" max_size={maxSize}");
            args.Append($" quality={quality}");
            args.Append($" max_fps={maxFps}");
            args.Append($" connect_timeout_ms={kServerConnectTimeoutMs}");
            if (!string.IsNullOrEmpty(displayId))
                args.Append($" display_id={displayId}");
            if (Unsupported.IsDeveloperMode())
                args.Append(" log_level=debug");

            AndroidLogcatInternalLog.Log($"{m_Runtime.Tools.ADB.GetADBPath()} {args}");

            m_ServerProcess = new Process();
            var si = m_ServerProcess.StartInfo;
            si.FileName = m_Runtime.Tools.ADB.GetADBPath();
            si.Arguments = args.ToString();
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            // Both streams are drained asynchronously. The server logs to stdout and
            // stderr for its whole lifetime, and a pipe nobody reads eventually fills
            // and blocks the server.
            m_ServerProcess.OutputDataReceived += OnServerOutput;
            m_ServerProcess.ErrorDataReceived += OnServerOutput;
            m_ServerProcess.Start();
            m_ServerProcess.BeginOutputReadLine();
            m_ServerProcess.BeginErrorReadLine();
        }

        void OnServerOutput(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;
            lock (m_ServerLog)
                m_ServerLog.AppendLine(e.Data);
            AndroidLogcatInternalLog.Log(e.Data);
        }

        int SetupPortForward(IAndroidLogcatDevice device, string socketName)
        {
            // tcp:0 lets adb pick a free port and print it, so two Editors streaming
            // from two devices cannot collide on a hardcoded one.
            var output = m_Runtime.Tools.ADB.Run(new[]
            {
                $"-s {device.Id}",
                "forward",
                "tcp:0",
                $"localabstract:{socketName}"
            }, "Failed to set up an adb port forward for the live stream");

            if (!int.TryParse(output.Trim(), out var port) || port <= 0)
                throw new Exception($"Expected a port number from 'adb forward tcp:0', got '{output}'");

            AndroidLogcatInternalLog.Log($"Forwarding tcp:{port} to localabstract:{socketName}");
            return port;
        }

        // ------------------------------------------------------------------
        // Reader thread
        // ------------------------------------------------------------------

        void ReadFrames()
        {
            try
            {
                var stream = Connect();
                var header = new byte[kFrameHeaderSize];

                while (!m_Stop)
                {
                    ReadExactly(stream, header, kFrameHeaderSize);
                    // Bytes 0..7 are the presentation timestamp, unused: frames are
                    // displayed as they arrive rather than scheduled.
                    var width = ReadInt32BE(header, 8);
                    var height = ReadInt32BE(header, 12);
                    var size = ReadInt32BE(header, 16);

                    if (size <= 0 || size > kMaxFrameSize)
                        throw new IOException($"Frame size {size} is out of range, the stream is out of sync");

                    var payload = RentFrameBuffer(size);
                    ReadExactly(stream, payload, size);

                    lock (m_FrameLock)
                    {
                        // Only the newest frame is kept: if the Editor cannot keep up,
                        // showing the latest screen matters more than showing every
                        // frame. The frame being dropped goes back to the free list
                        // instead of to the GC - the main thread never saw it, so
                        // nothing else can be holding it.
                        ReturnFrameBuffer(m_PendingFrame);

                        m_PendingFrame = payload;
                        m_PendingFrameSize = size;
                        m_PendingWidth = width;
                        m_PendingHeight = height;
                        m_ReceivedBytes += size;
                        m_ReceivedFrames++;
                    }
                }
            }
            catch (Exception ex)
            {
                // A read failing after Stop was requested is just the connection we
                // closed ourselves.
                if (!m_Stop)
                    m_ReaderError = ex.Message;
            }
            finally
            {
                m_StreamEnded = true;
            }
        }

        /// <summary>
        /// Connects to the forwarded port and validates the stream header, retrying
        /// until the server has created its socket. 'adb forward' succeeds whether or
        /// not anything is listening on the device yet, so an early attempt shows up as
        /// a connection that is immediately closed rather than as a refused connect.
        /// </summary>
        NetworkStream Connect()
        {
            var deadline = DateTime.Now.AddMilliseconds(kConnectTimeoutMs);
            Exception lastFailure = null;

            while (!m_Stop)
            {
                TcpClient client = null;
                try
                {
                    client = new TcpClient { NoDelay = true };
                    lock (m_ConnectionLock)
                    {
                        if (m_Stop)
                            throw new OperationCanceledException();
                        // Published before connecting, so that a Stop arriving now can
                        // close the socket and break us out of the attempt.
                        m_Client = client;
                    }

                    client.Connect(IPAddress.Loopback, m_ForwardedPort);
                    var stream = client.GetStream();
                    ValidateStreamHeader(stream);

                    lock (m_ConnectionLock)
                        m_Stream = stream;
                    AndroidLogcatInternalLog.Log($"Live stream connected on port {m_ForwardedPort}");
                    return stream;
                }
                catch (ProtocolMismatchException)
                {
                    // Retrying cannot help: we did reach the server and disagree with it.
                    throw;
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    lock (m_ConnectionLock)
                    {
                        m_Client = null;
                        m_Stream = null;
                    }
                    try
                    {
                        client?.Close();
                    }
                    catch (Exception)
                    {
                        // Nothing useful to do about a failure to close a failed socket.
                    }

                    if (m_Stop || DateTime.Now >= deadline)
                        break;
                    Thread.Sleep(kConnectRetryDelayMs);
                }
            }

            if (m_Stop)
                throw new OperationCanceledException();

            var reason = lastFailure != null ? $": {lastFailure.Message}" : string.Empty;
            throw new IOException($"Timed out after {kConnectTimeoutMs} ms connecting to the server on the device{reason}");
        }

        void ValidateStreamHeader(NetworkStream stream)
        {
            var header = new byte[kStreamHeaderSize];
            ReadExactly(stream, header, kStreamHeaderSize);

            var magic = (uint)ReadInt32BE(header, 0);
            if (magic != kProtocolMagic)
                throw new ProtocolMismatchException($"Expected stream magic 0x{kProtocolMagic:X8} but got 0x{magic:X8}, this is not the live stream server");

            var version = ReadInt32BE(header, 4);
            if (version != kProtocolVersion)
            {
                throw new ProtocolMismatchException(
                    $"The server on the device speaks protocol version {version}, this Editor expects {kProtocolVersion}.\n" +
                    $"Rebuild {kServerJarName} with 'gradlew dexJar' in External/UnityLogcatServer.");
            }

            var codec = ReadInt32BE(header, 8);
            if (codec != kCodecMjpeg)
                throw new ProtocolMismatchException($"The server is sending codec {codec}, which this Editor cannot decode");

            var flags = ReadInt32BE(header, 12);
            m_ControlSupported = (flags & kFlagControlSupported) != 0;
            if (!m_ControlSupported)
                AndroidLogcatInternalLog.Log("The server cannot inject input, the live stream will be view only");

            m_ServerPid = ReadInt32BE(header, 16);
        }

        static void ReadExactly(Stream stream, byte[] buffer, int count)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    throw new EndOfStreamException("The device closed the live stream connection");
                offset += read;
            }
        }

        static int ReadInt32BE(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24)
                | (buffer[offset + 1] << 16)
                | (buffer[offset + 2] << 8)
                | buffer[offset + 3];
        }

        /// <summary>
        /// Reaching a server we cannot talk to, as opposed to not reaching one yet.
        /// Retrying a connect makes sense for the latter and never for the former.
        /// </summary>
        class ProtocolMismatchException : Exception
        {
            public ProtocolMismatchException(string message) : base(message)
            {
            }
        }

        // ------------------------------------------------------------------
        // Teardown
        // ------------------------------------------------------------------

        void CloseConnection()
        {
            lock (m_ConnectionLock)
            {
                try
                {
                    m_Stream?.Close();
                    m_Client?.Close();
                }
                catch (Exception ex)
                {
                    AndroidLogcatInternalLog.Log($"Failed to close the live stream connection: {ex.Message}");
                }
                m_Stream = null;
                m_Client = null;
            }
        }

        void KillServerProcess()
        {
            var process = m_ServerProcess;
            m_ServerProcess = null;
            if (process == null)
                return;

            try
            {
                // Closing the connection makes the server exit by itself, so give it a
                // moment before killing it: a clean exit releases the mirrored display
                // on the device instead of leaving it to the kernel.
                if (!process.WaitForExit(1000))
                {
                    AndroidLogcatInternalLog.Log("Live stream server did not exit on its own, killing it");
                    process.Kill();
                    process.WaitForExit();
                }
                AndroidLogcatInternalLog.Log($"Live stream server exited with code {process.ExitCode}");
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to stop the live stream server: {ex.Message}");
            }
            finally
            {
                process.Close();
            }
        }

        /// <summary>
        /// Deletes jars left behind by sessions that never got to clean up after
        /// themselves - an Editor killed mid-stream - and the fixed name that versions
        /// before the per-session path used.
        /// <para>
        /// Safe even if a server is still running from one of them: unlinking a jar does
        /// not disturb a process already executing it, because the runtime keeps the
        /// file it opened. That was verified on device rather than assumed, on both
        /// Android 16 and Android 8.1.
        /// </para>
        /// </summary>
        void RemoveStaleServerJars(IAndroidLogcatDevice device)
        {
            DeleteOnDevice(device, $"{kServerDeviceFolder}/{kServerDeviceNamePrefix}*.jar");
        }

        /// <summary>
        /// Deletes this session's jar from the device. Unlinking it is safe even if the
        /// server somehow outlived us - the file stays alive for whoever has it open -
        /// and skipping it would leave 17 KB behind on the device per stream.
        /// </summary>
        void RemoveServerJar()
        {
            var path = m_ServerDevicePath;
            m_ServerDevicePath = null;
            DeleteOnDevice(m_Device, path);
        }

        /// <summary>
        /// Deletes files on the device, one path or a glob, and never fails: tidying up
        /// is not worth losing a stream over, and what is left behind if it does fail is
        /// a small file in a temporary folder.
        /// </summary>
        void DeleteOnDevice(IAndroidLogcatDevice device, string target)
        {
            if (device == null || string.IsNullOrEmpty(target))
                return;

            try
            {
                m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {device.Id}",
                    "shell",
                    // Quoted so the target reaches the device's shell whole, glob and
                    // all, rather than anything on this side of adb taking an interest
                    // in it. rm -f is silent when nothing matches.
                    $"\"rm -f {target}\""
                }, $"Failed to delete {target} from the device");
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
            }
        }

        void RemovePortForward()
        {
            if (m_ForwardedPort <= 0)
                return;

            var port = m_ForwardedPort;
            var device = m_Device;
            m_ForwardedPort = -1;
            if (device == null)
                return;

            try
            {
                m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {device.Id}",
                    "forward",
                    "--remove",
                    $"tcp:{port}"
                }, $"Failed to remove the adb port forward for tcp:{port}");
            }
            catch (Exception ex)
            {
                // Not worth failing the stop over: the forward goes away with the adb
                // server, and a leaked one only occupies a local port.
                AndroidLogcatInternalLog.Log(ex.Message);
            }
        }

        /// <summary>
        /// A buffer at least <paramref name="size"/> bytes long, reused if one that big
        /// is free. Called only from the reader thread.
        /// </summary>
        byte[] RentFrameBuffer(int size)
        {
            lock (m_FrameLock)
            {
                while (m_FreeFrameBuffers.Count > 0)
                {
                    var buffer = m_FreeFrameBuffers.Pop();
                    if (buffer.Length >= size)
                        return buffer;
                    // Too small, because frames have grown - a rotation, or simply a
                    // busier screen. Dropped, and the rounding up below replaces it.
                }
            }

            // Rounded up so that frames creeping up in size do not reallocate every
            // time: JPEG sizes vary frame to frame even at a fixed resolution.
            return new byte[Mathf.NextPowerOfTwo(size)];
        }

        /// <summary>
        /// Gives a buffer back, from either thread. Null is accepted, so returning
        /// whatever happened to be in the pending slot needs no check at the call site.
        /// </summary>
        void ReturnFrameBuffer(byte[] buffer)
        {
            if (buffer == null)
                return;

            lock (m_FrameLock)
            {
                // Over the cap only if a buffer has leaked somewhere, in which case the
                // extra one is better dropped than kept forever.
                if (m_FreeFrameBuffers.Count < kMaxFrameBuffers)
                    m_FreeFrameBuffers.Push(buffer);
            }
        }

        void DestroyTexture()
        {
            if (m_Texture == null)
                return;
            UnityEngine.Object.DestroyImmediate(m_Texture);
            m_Texture = null;
        }

        void AppendError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (m_Errors.Length > 0)
                m_Errors.AppendLine();
            m_Errors.AppendLine(message);
        }

        void AppendServerLog()
        {
            string log;
            lock (m_ServerLog)
                log = m_ServerLog.ToString();
            if (string.IsNullOrEmpty(log))
                return;
            m_Errors.AppendLine();
            m_Errors.AppendLine("Server output:");
            m_Errors.Append(log);
        }

        // ------------------------------------------------------------------
        // GUI
        // ------------------------------------------------------------------

        internal void DoGUI(Rect rc)
        {
            // Allocated on every pass, before any early return: skipping it on some
            // frames would shift control ids between the Layout and Repaint passes and
            // trip "GUI id mismatch" warnings.
            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);

            if (m_Errors.Length > 0)
            {
                EditorGUI.HelpBox(rc, m_Errors.ToString(), MessageType.Error);
                if (m_FailureType == FailureType.JarNotFound)
                {
                    // TODO: make it nice
                    var path = GetServerGradleProjectPath();
                    if (Directory.Exists(path))
                    {
                        if (GUILayout.Button($"Build Jar at '{path}'"))
                        {
                            RebuildServerJar(GetServerGradleProjectPath());
                            RestartStreaming(m_Device);
                        }
                    }
                }
                  
                return;
            }

            if (m_Texture == null)
            {
                var message = IsStreaming
                    ? "Starting the stream on the device..."
                    // Selecting the Live row is what starts a stream, so reselecting it
                    // is how one is restarted after it has stopped.
                    : "The live stream is not running. Select another row, then Live, to start it again.";
                EditorGUI.HelpBox(rc, message, MessageType.Info);
                return;
            }

            // The info column is reserved before the image is fitted, so that the image
            // is never drawn underneath it.
            var statsWidth = IsStreaming ? Mathf.Min(kStatsWidth, rc.width * 0.4f) : 0;
            var imageArea = new Rect(rc.x, rc.y, Mathf.Max(0, rc.width - statsWidth), rc.height);

            // Fitted explicitly rather than letting ScaleMode.ScaleToFit do it, because
            // the letterboxed rect is also what mouse positions are mapped through.
            var videoRect = FitRect(imageArea, (float)m_Texture.width / m_Texture.height);

            HandleTouchInput(controlId, videoRect);
            HandleKeyboardInput(controlId);

            GUI.DrawTexture(videoRect, m_Texture);

            if (statsWidth > 0)
            {
                // Attached to the image rather than to the right edge of the area: the
                // image is centred in what is left over, so the gap beside it varies.
                var statsRect = new Rect(
                    videoRect.xMax + kStatsMargin,
                    videoRect.y,
                    Mathf.Max(0, rc.xMax - videoRect.xMax - kStatsMargin),
                    videoRect.height);
                DoStatsGUI(statsRect);
            }
        }

        void DoStatsGUI(Rect rc)
        {
            const float kLabelWidth = 80;
            var y = rc.y;

            DoStatsRow(rc, kLabelWidth, ref y, Styles.StreamSize, $"{m_FrameWidth}x{m_FrameHeight}");
            DoStatsRow(rc, kLabelWidth, ref y, Styles.FrameRate, $"{m_Fps:0.0} fps");
            DoStatsRow(rc, kLabelWidth, ref y, Styles.Bandwidth, $"{m_Mbps:0.00} Mbps");
            // Listed whether or not it works: without the row there is nothing in the
            // window to say the view is interactive at all. One row rather than separate
            // Touch and Keyboard ones because the server reports a single capability
            // covering both, so the two could never disagree. The column is too narrow
            // for how to use them, so that lives in the tooltip.
            DoStatsRow(rc, kLabelWidth, ref y, Styles.Input,
                m_ControlSupported ? "Supported" : "Unsupported");

            y += kNavigationSpacing;
            DoNavigationGUI(rc, ref y);
            DoDebuggingGUI(rc, kLabelWidth, ref y);
        }

        /// <summary>
        /// Android's Back / Home / Overview buttons.
        /// <para>
        /// On a device with the three-button navigation bar these are also just tappable
        /// in the mirrored image, but on one using gesture navigation there is no bar to
        /// tap - so without these there is no way to leave an app from the live view.
        /// </para>
        /// </summary>
        void DoNavigationGUI(Rect rc, ref float y)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (y + height > rc.yMax)
                return;

            EditorGUI.BeginDisabledGroup(!CanSendInput);

            // Fixed width, rather than a third of the column each: these hold a single
            // glyph, so stretching them to fill the column just looks wrong. Narrowed
            // only if the column itself cannot fit three of them. Joined into one group,
            // as the same row is in the Inputs window.
            var width = Mathf.Min(kNavigationButtonWidth, Mathf.Floor(rc.width / 3));
            if (GUI.Button(new Rect(rc.x, y, width, height), Styles.Back, EditorStyles.miniButtonLeft))
                SendKeyPress(AndroidKeyCode.BACK);
            if (GUI.Button(new Rect(rc.x + width, y, width, height), Styles.Home, EditorStyles.miniButtonMid))
                SendKeyPress(AndroidKeyCode.HOME);
            if (GUI.Button(new Rect(rc.x + width * 2, y, width, height), Styles.Recents, EditorStyles.miniButtonRight))
                SendKeyPress(AndroidKeyCode.APP_SWITCH);

            EditorGUI.EndDisabledGroup();
            y += height;
        }

        static void DoStatsRow(Rect rc, float labelWidth, ref float y, GUIContent name, string value,
            string valueTooltip = null)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (y + height > rc.yMax)
                return;

            GUI.Label(new Rect(rc.x, y, labelWidth, height), name, EditorStyles.miniLabel);
            // The column is narrow enough that long values clip, so the tooltip carries
            // the full text where that matters.
            GUI.Label(new Rect(rc.x + labelWidth, y, Mathf.Max(0, rc.width - labelWidth), height),
                new GUIContent(value, valueTooltip ?? name.tooltip), EditorStyles.miniLabel);
            y += height;
        }

        /// <summary>Largest rect of the given aspect ratio that fits inside the container.</summary>
        static Rect FitRect(Rect container, float aspect)
        {
            if (container.width <= 0 || container.height <= 0 || aspect <= 0)
                return container;

            if (aspect > container.width / container.height)
            {
                var height = container.width / aspect;
                return new Rect(container.x, container.y + (container.height - height) * 0.5f, container.width, height);
            }

            var width = container.height * aspect;
            return new Rect(container.x + (container.width - width) * 0.5f, container.y, width, container.height);
        }

        // ------------------------------------------------------------------
        // Touch forwarding
        // ------------------------------------------------------------------

        void HandleTouchInput(int controlId, Rect videoRect)
        {
            var e = Event.current;

            if (!CanSendInput)
            {
                // Control switched off, or the stream dropped, in the middle of a drag.
                // The device still believes a finger is down, so let go of it - which
                // deliberately bypasses the CanSendTouch gate that just failed.
                if (m_TouchDown)
                {
                    SendTouchAt(TouchAction.Cancel, videoRect, e.mousePosition);
                    ReleaseTouch(controlId);
                }
                return;
            }

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !videoRect.Contains(e.mousePosition))
                        break;
                    // Taking the hot control is what routes the rest of the drag here,
                    // including the part that happens outside the rect.
                    GUIUtility.hotControl = controlId;
                    // Also takes keyboard focus, so a click is all it takes before typing.
                    GUIUtility.keyboardControl = controlId;
                    m_TouchDown = true;
                    SendTouchAt(TouchAction.Down, videoRect, e.mousePosition);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!m_TouchDown)
                        break;
                    SendTouchAt(TouchAction.Move, videoRect, e.mousePosition);
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (!m_TouchDown)
                        break;
                    SendTouchAt(TouchAction.Up, videoRect, e.mousePosition);
                    ReleaseTouch(controlId);
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    // No focus or hot control needed: a wheel acts on whatever the
                    // pointer is over, on the device as much as in the Editor.
                    if (!videoRect.Contains(e.mousePosition))
                        break;
                    SendScrollAt(videoRect, e.mousePosition, e.delta);
                    e.Use();
                    break;
            }

            // Losing the mouse mid-drag would otherwise leave the finger down for good.
            if (m_TouchDown && e.type == EventType.MouseLeaveWindow)
            {
                SendTouchAt(TouchAction.Cancel, videoRect, e.mousePosition);
                ReleaseTouch(controlId);
            }
        }

        void ReleaseTouch(int controlId)
        {
            m_TouchDown = false;
            if (GUIUtility.hotControl == controlId)
                GUIUtility.hotControl = 0;
        }

        /// <summary>
        /// Sends a touch at a position in normalized display coordinates, (0,0) being the
        /// top left of the device screen. Does nothing unless the stream is up, the server
        /// supports injection and control is enabled.
        /// </summary>
        internal void SendTouch(TouchAction action, float normalizedX, float normalizedY)
        {
            if (!CanSendInput)
                return;
            SendTouchMessage(action, normalizedX, normalizedY);
        }

        /// <summary>
        /// Sends a named key, e.g. <see cref="AndroidKeyCode.Back"/>. For typed
        /// characters use <see cref="SendText"/> instead, which handles layouts.
        /// </summary>
        internal void SendKey(KeyAction action, AndroidKeyCode keyCode, int metaState = 0)
        {
            if (!CanSendInput)
                return;
            SendKeyMessage(action, keyCode, metaState);
        }

        /// <summary>
        /// Presses and releases a key, for callers that have no press and release of
        /// their own to mirror - a toolbar button, say.
        /// </summary>
        internal void SendKeyPress(AndroidKeyCode keyCode, int metaState = 0)
        {
            SendKey(KeyAction.Down, keyCode, metaState);
            SendKey(KeyAction.Up, keyCode, metaState);
        }

        /// <summary>
        /// Sends a scroll at a position in normalized display coordinates. Magnitudes are
        /// in wheel notches: positive vertical scrolls away from the user, positive
        /// horizontal to the right.
        /// </summary>
        internal void SendScroll(float normalizedX, float normalizedY,
            float horizontalNotches, float verticalNotches)
        {
            if (!CanSendInput)
                return;
            SendScrollMessage(normalizedX, normalizedY, horizontalNotches, verticalNotches);
        }

        /// <summary>Types text on the device.</summary>
        internal void SendText(string text)
        {
            if (!CanSendInput || string.IsNullOrEmpty(text))
                return;
            SendTextMessage(text);
        }

        void SendTouchAt(TouchAction action, Rect videoRect, Vector2 mousePosition)
        {
            // Clamped, not rejected: a swipe that overshoots the edge of the view should
            // still read as a swipe to the edge of the screen. GUI y grows downward and
            // so does the device y, so there is nothing to flip.
            var x = Mathf.Clamp01((mousePosition.x - videoRect.x) / videoRect.width);
            var y = Mathf.Clamp01((mousePosition.y - videoRect.y) / videoRect.height);
            SendTouchMessage(action, x, y);
        }

        void SendScrollAt(Rect videoRect, Vector2 mousePosition, Vector2 delta)
        {
            var x = Mathf.Clamp01((mousePosition.x - videoRect.x) / videoRect.width);
            var y = Mathf.Clamp01((mousePosition.y - videoRect.y) / videoRect.height);

            // Unity's scroll delta grows downward, where Android's VSCROLL is notches
            // away from the user, so the vertical sign flips. Horizontal is passed
            // through: both count rightward as positive.
            SendScrollMessage(x, y,
                delta.x / kUnityScrollLinesPerNotch,
                -delta.y / kUnityScrollLinesPerNotch);
        }

        void SendTouchMessage(TouchAction action, float x, float y)
        {
            var nx = (int)Mathf.Round(x * kNormalizedMax);
            var ny = (int)Mathf.Round(y * kNormalizedMax);
            // Full pressure. The server drops it to 0 for an Up by itself.
            var pressure = (int)kNormalizedMax;

            var message = m_ControlMessage;
            message[0] = (byte)ControlMessage.Touch;
            message[1] = (byte)action;
            message[2] = 0; // pointer id - a mouse is a single finger
            message[3] = (byte)(nx >> 8);
            message[4] = (byte)nx;
            message[5] = (byte)(ny >> 8);
            message[6] = (byte)ny;
            message[7] = (byte)(pressure >> 8);
            message[8] = (byte)pressure;

            SendControlMessage(message, 9, "touch");
        }

        void SendScrollMessage(float x, float y, float hScroll, float vScroll)
        {
            var h = ToScrollFixedPoint(hScroll);
            var v = ToScrollFixedPoint(vScroll);
            // Rounded away to nothing - a trackpad twitch, or a delta of zero on an axis
            // the mouse does not have. The server would ignore it anyway.
            if (h == 0 && v == 0)
                return;

            var nx = (int)Mathf.Round(x * kNormalizedMax);
            var ny = (int)Mathf.Round(y * kNormalizedMax);

            var message = m_ControlMessage;
            message[0] = (byte)ControlMessage.Scroll;
            message[1] = (byte)(nx >> 8);
            message[2] = (byte)nx;
            message[3] = (byte)(ny >> 8);
            message[4] = (byte)ny;
            message[5] = (byte)(h >> 8);
            message[6] = (byte)h;
            message[7] = (byte)(v >> 8);
            message[8] = (byte)v;

            SendControlMessage(message, 9, "scroll");
        }

        static short ToScrollFixedPoint(float notches)
        {
            return (short)Mathf.Clamp(Mathf.Round(notches * kScrollScale),
                short.MinValue, short.MaxValue);
        }

        void SendKeyMessage(KeyAction action, AndroidKeyCode keyCode, int metaState)
        {
            var message = m_ControlMessage;
            message[0] = (byte)ControlMessage.Key;
            message[1] = (byte)action;
            WriteInt32BE(message, 2, (int)keyCode);
            WriteInt32BE(message, 6, metaState);

            SendControlMessage(message, 10, "key");
        }

        void SendTextMessage(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length > kMaxTextBytes)
            {
                AndroidLogcatInternalLog.Log($"Not sending {bytes.Length} bytes of text, the limit is {kMaxTextBytes}");
                return;
            }

            // Length prefixed, so the server stays in sync even on a message it decides
            // to ignore. Allocated per message rather than reusing a buffer: this only
            // happens on a keystroke or a paste.
            var message = new byte[3 + bytes.Length];
            message[0] = (byte)ControlMessage.Text;
            message[1] = (byte)(bytes.Length >> 8);
            message[2] = (byte)bytes.Length;
            Array.Copy(bytes, 0, message, 3, bytes.Length);

            SendControlMessage(message, message.Length, "text");
        }

        void SendControlMessage(byte[] message, int length, string what)
        {
            NetworkStream stream;
            lock (m_ConnectionLock)
                stream = m_Stream;
            if (stream == null)
                return;

            try
            {
                stream.Write(message, 0, length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                // The reader thread watches the same connection and will report the
                // failure properly, so this only needs to avoid throwing out of OnGUI -
                // and to not log the same thing once per mouse move.
                if (!m_ControlWriteFailed)
                {
                    m_ControlWriteFailed = true;
                    AndroidLogcatInternalLog.Log($"Failed to send a {what} event: {ex.Message}");
                }
            }
        }

        static void WriteInt32BE(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        // ------------------------------------------------------------------
        // Keyboard forwarding
        // ------------------------------------------------------------------

        void HandleKeyboardInput(int controlId)
        {
            if (!CanSendInput || GUIUtility.keyboardControl != controlId)
                return;

            var e = Event.current;
            if (e.type != EventType.KeyDown && e.type != EventType.KeyUp)
                return;

            // Select all, copy and paste act on the device: they are text editing where
            // the text is, and they do nothing in this window otherwise. The device's
            // own clipboard is what is copied to and pasted from - nothing is exchanged
            // with the Editor's clipboard.
            if (TryMapEditingShortcut(e, out var editingKeyCode))
            {
                // Forced to Ctrl even when the user pressed Cmd: Android has no Command
                // modifier, and META_CTRL_ON is what a text field acts on.
                SendKeyMessage(e.type == EventType.KeyDown ? KeyAction.Down : KeyAction.Up,
                    editingKeyCode, MetaState(e.modifiers) | kMetaCtrlOn);
                e.Use();
                return;
            }

            // Every other Editor shortcut keeps working: Ctrl/Cmd combinations are not
            // forwarded, so Ctrl+S still saves rather than going to the device.
            if ((e.modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0)
                return;

            if (TryMapKeyCode(e.keyCode, out var androidKeyCode))
            {
                SendKeyMessage(e.type == EventType.KeyDown ? KeyAction.Down : KeyAction.Up,
                    androidKeyCode, MetaState(e.modifiers));
                e.Use();
                return;
            }

            // Anything printable goes as text rather than as a keycode. Unity reports a
            // printable key twice - once with a keyCode and once with a character - and
            // only the character knows about the keyboard layout, so letting the device
            // work out the keystrokes from the character is what makes punctuation and
            // non-US layouts come out right.
            if (e.type == EventType.KeyDown && e.character != '\0' && !char.IsControl(e.character))
            {
                SendTextMessage(e.character.ToString());
                e.Use();
            }
        }

        /// <summary>
        /// Named keys that have no character to type. Everything else - letters, digits,
        /// punctuation - is left to the text path.
        /// </summary>
        static bool TryMapKeyCode(KeyCode keyCode, out AndroidKeyCode androidKeyCode)
        {
            switch (keyCode)
            {
                // Escape is the device's BACK rather than Android's ESCAPE: on a phone
                // that is what "go back" means, and it is the reason to press it.
                case KeyCode.Escape: androidKeyCode = AndroidKeyCode.BACK; return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: androidKeyCode = AndroidKeyCode.ENTER; return true;
                case KeyCode.Backspace: androidKeyCode = AndroidKeyCode.DEL; return true;
                case KeyCode.Delete: androidKeyCode = AndroidKeyCode.FORWARD_DEL; return true;
                case KeyCode.Tab: androidKeyCode = AndroidKeyCode.TAB; return true;
                case KeyCode.UpArrow: androidKeyCode = AndroidKeyCode.DPAD_UP; return true;
                case KeyCode.DownArrow: androidKeyCode = AndroidKeyCode.DPAD_DOWN; return true;
                case KeyCode.LeftArrow: androidKeyCode = AndroidKeyCode.DPAD_LEFT; return true;
                case KeyCode.RightArrow: androidKeyCode = AndroidKeyCode.DPAD_RIGHT; return true;
                case KeyCode.Home: androidKeyCode = AndroidKeyCode.MOVE_HOME; return true;
                case KeyCode.End: androidKeyCode = AndroidKeyCode.MOVE_END; return true;
                case KeyCode.PageUp: androidKeyCode = AndroidKeyCode.PAGE_UP; return true;
                case KeyCode.PageDown: androidKeyCode = AndroidKeyCode.PAGE_DOWN; return true;
                default: androidKeyCode = default; return false;
            }
        }

        /// <summary>
        /// The Ctrl/Cmd chords that are forwarded to the device rather than left to the
        /// Editor: select all, copy and paste. Only the bare chord, so Ctrl+Shift+A and
        /// anything with Alt still belong to the Editor.
        /// </summary>
        internal static bool TryMapEditingShortcut(Event e, out AndroidKeyCode androidKeyCode)
        {
            androidKeyCode = default;

            if ((e.modifiers & (EventModifiers.Control | EventModifiers.Command)) == 0)
                return false;
            if ((e.modifiers & (EventModifiers.Shift | EventModifiers.Alt)) != 0)
                return false;

            switch (e.keyCode)
            {
                case KeyCode.A: androidKeyCode = AndroidKeyCode.A; return true;
                case KeyCode.C: androidKeyCode = AndroidKeyCode.C; return true;
                case KeyCode.V: androidKeyCode = AndroidKeyCode.V; return true;
                default: return false;
            }
        }

        static int MetaState(EventModifiers modifiers)
        {
            var meta = 0;
            if ((modifiers & EventModifiers.Shift) != 0)
                meta |= kMetaShiftOn;
            if ((modifiers & EventModifiers.Alt) != 0)
                meta |= kMetaAltOn;
            if ((modifiers & EventModifiers.Control) != 0)
                meta |= kMetaCtrlOn;
            return meta;
        }

        /// <summary>
        /// Opens the Android Logcat window showing only this server's process, the
        /// equivalent of <c>adb logcat --pid=&lt;server pid&gt;</c>. Tag filtering is left
        /// as the user set it.
        /// </summary>
        void ShowServerLogcat()
        {
            var window = AndroidLogcatConsoleWindow.ShowNewOrExisting();
            if (window == null)
                return;

            // Logcat follows the runtime-wide device selection, so filtering by a process
            // id means nothing without selecting the device that process is on first.
            if (m_Device != null)
                m_Runtime.DeviceQuery.SelectDevice(m_Device);

            window.FilterByProcessId(m_ServerPid);
        }

        /// <summary>
        /// Extra detail for diagnosing the stream, below the navigation buttons. Touch
        /// support is not repeated here - the rows above already report it.
        /// </summary>
        void DoDebuggingGUI(Rect rc, float labelWidth, ref float y)
        {
            if (!Unsupported.IsDeveloperMode())
                return;

            var height = EditorGUIUtility.singleLineHeight;
            y += kNavigationSpacing;
            if (y + height > rc.yMax)
                return;

            GUI.Label(new Rect(rc.x, y, rc.width, height), Styles.DeveloperMode, EditorStyles.miniBoldLabel);
            y += height;

            DoStatsRow(rc, labelWidth, ref y, Styles.Socket,
                string.IsNullOrEmpty(m_SocketName) ? "-" : m_SocketName, m_SocketName);
            DoStatsRow(rc, labelWidth, ref y, Styles.ForwardedPort,
                m_ForwardedPort > 0 ? m_ForwardedPort.ToString() : "-");
            DoStatsRow(rc, labelWidth, ref y, Styles.ServerOnDevice,
                string.IsNullOrEmpty(m_ServerDevicePath) ? "-" : m_ServerDevicePath, m_ServerDevicePath);
            DoStatsRow(rc, labelWidth, ref y, Styles.ServerPid,
                m_ServerPid > 0 ? m_ServerPid.ToString() : "-");

            if (y + height > rc.yMax)
                return;

            // Everything the server logs goes to logcat as well, so there is no button
            // for the copy the Editor captures from the adb shell - that copy is kept
            // only because a server that dies before it has a pid leaves nothing for
            // the Logcat window to filter on, and it ends up in Errors instead.
            var buttonWidth = Mathf.Min(kDebugButtonWidth, rc.width);

            EditorGUI.BeginDisabledGroup(m_ServerPid <= 0 || m_Device == null);
            if (GUI.Button(new Rect(rc.x, y, buttonWidth, height),
                Styles.ShowServerLogcat, EditorStyles.miniButton))
            {
                ShowServerLogcat();
            }
            EditorGUI.EndDisabledGroup();

            y += height;

            if (y + height > rc.yMax)
                return;

            var gradleProject = GetServerGradleProjectPath();
            EditorGUI.BeginDisabledGroup(gradleProject == null);
            if (GUI.Button(new Rect(rc.x, y, buttonWidth, height),
                Styles.RebuildJar, EditorStyles.miniButton))
            {
                RebuildServerJar(gradleProject);
            }
            EditorGUI.EndDisabledGroup();

            y += height;
        }

        /// <summary>
        /// The Gradle project that builds the server, which lives beside the package in
        /// its own repository - <c>&lt;repo&gt;/External/UnityLogcatServer</c> - and not
        /// at all in a package installed from a registry. Null when it is not there.
        /// </summary>
        static string GetServerGradleProjectPath()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AndroidLogcatLiveStream).Assembly);
            if (package == null)
                return null;

            var path = Path.GetFullPath(Path.Combine(package.resolvedPath, "..",
                "External", "UnityLogcatServer"));
            return File.Exists(Path.Combine(path, "build.gradle")) ? path : null;
        }

        /// <summary>
        /// Builds the server jar and, if a stream is up, restarts it so the device runs
        /// the new one and the Logcat window follows it. Developer-mode only: it is the
        /// edit-build-run loop for the server, which is otherwise a trip to a terminal.
        /// </summary>
        void RebuildServerJar(string gradleProject)
        {
            if (gradleProject == null)
                return;

            if (!AndroidLogcatUtilities.RunGradle(gradleProject, "dexJar"))
                return;

            var wasStreaming = IsStreaming;
            UnityEngine.Debug.Log("Live stream server jar rebuilt" +
                (wasStreaming ? ", restarting the stream" : ""));

            if (!wasStreaming)
                return;

            RestartStreaming(m_Device);
            // Armed after the restart, so that the state reset inside StartStreaming
            // does not clear it, and only if that restart actually took: a stream that
            // failed to start has no server to show, and Shutdown disarms this anyway.
            m_ShowLogcatWhenServerStarts = IsStreaming;
        }

        /// <summary>
        /// Stops and starts the stream against the same device, keeping the caller's
        /// completion callback. Does nothing when no stream is running.
        /// </summary>
        internal void RestartStreaming(IAndroidLogcatDevice device)
        {
            var onStopped = m_OnStopLiveStream;
            StopStreaming();
            if (device != null)
                StartStreaming(device, onStopped);
        }
    }
}
