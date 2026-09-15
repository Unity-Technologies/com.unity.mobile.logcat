using System;
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

        // Must stay in step with External/UnityLogcatServer: Protocol.java and the
        // serverProtocolVersion / serverSocketName / serverDevicePath entries in
        // gradle.properties. The server sends its version in the stream header, so a
        // mismatch is reported rather than misparsed.
        const uint kProtocolMagic = 0x554C5331; // "ULS1"
        const int kProtocolVersion = 1;
        const int kCodecMjpeg = 1;
        const int kStreamHeaderSize = 12; // magic + version + codec
        const int kFrameHeaderSize = 20;  // ptsUs + width + height + payloadSize

        const string kServerJarName = "unity-logcat-server.jar";
        const string kServerDevicePath = "/data/local/tmp/unity-logcat-server.jar";
        const string kServerMainClass = "com.unity.android.logcat.server.Server";
        const string kServerExternalFolder = "External~";

        // Stream defaults. Not exposed in the UI yet.
        const int kMaxSize = 1024;
        const int kQuality = 70;
        const int kMaxFps = 30;

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

        AndroidLogcatRuntimeBase m_Runtime;
        IAndroidLogcatDevice m_Device;
        Action<Result> m_OnStopLiveStream;

        Process m_ServerProcess;
        readonly StringBuilder m_ServerLog = new StringBuilder();
        readonly StringBuilder m_Errors = new StringBuilder();
        string m_SocketName;
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
        int m_PendingWidth;
        int m_PendingHeight;
        long m_ReceivedBytes;
        int m_ReceivedFrames;

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
            lock (m_ServerLog)
                m_ServerLog.Clear();
            DestroyTexture();

            m_Device = device;
            m_OnStopLiveStream = onStopLiveStream;
            m_Stop = false;
            m_ReaderError = null;
            m_StreamEnded = false;
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
                m_ReceivedBytes = 0;
                m_ReceivedFrames = 0;
            }

            try
            {
                var jarPath = GetServerJarPath();
                PushServer(device, jarPath);

                // Unique per session, so that a server left over from a previous run
                // cannot own the name we are about to listen on.
                m_SocketName = "unity_logcat_server_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                StartServerProcess(device, maxSize ?? kMaxSize, quality ?? kQuality, maxFps ?? kMaxFps, displayId);
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

            // The connection goes first: closing it is what unblocks a reader thread
            // parked in a read, so the join below does not have to wait it out.
            CloseConnection();

            var thread = m_ReaderThread;
            m_ReaderThread = null;
            if (thread != null && !thread.Join(TimeSpan.FromSeconds(2)))
                AndroidLogcatInternalLog.Log("Live stream reader thread did not stop in time");

            KillServerProcess();
            RemovePortForward();

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
                // The server closed the connection on its own - a display that went
                // away, or the process being killed from outside.
                Shutdown(Result.Success);
            }
        }

        void ApplyPendingFrame()
        {
            byte[] frame;
            int width, height;
            long bytes;
            int frames;

            lock (m_FrameLock)
            {
                frame = m_PendingFrame;
                m_PendingFrame = null;
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
                if (m_Texture.LoadImage(frame))
                {
                    m_FrameWidth = width;
                    m_FrameHeight = height;
                }
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

        static string GetServerJarPath()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AndroidLogcatLiveStream).Assembly);
            if (package == null)
                throw new FileNotFoundException($"Couldn't locate the Android Logcat package to find {kServerJarName} in it.");

            var path = Path.Combine(package.resolvedPath, kServerExternalFolder, kServerJarName);
            if (!File.Exists(path))
            {
                // Expected during development: the jar is a build output and is not
                // committed, so a fresh clone does not have one yet.
                throw new FileNotFoundException(
                    $"{kServerJarName} is missing from the package, live streaming is unavailable.\n" +
                    $"Expected it at {path}\n" +
                    "Build it by running 'gradlew dexJar' in External/UnityLogcatServer.");
            }
            return path;
        }

        void PushServer(IAndroidLogcatDevice device, string jarPath)
        {
            AndroidLogcatInternalLog.Log($"Pushing {jarPath} to {kServerDevicePath}");
            // Pushed on every start rather than only when missing: it is 13 KB, and it
            // rules out a stale jar from an older Editor being left on the device.
            m_Runtime.Tools.ADB.Run(new[]
            {
                $"-s {device.Id}",
                "push",
                $"\"{jarPath}\"",
                kServerDevicePath
            }, $"Failed to push {kServerJarName} to the device");
        }

        void StartServerProcess(IAndroidLogcatDevice device, int maxSize, int quality, int maxFps, string displayId)
        {
            var args = new StringBuilder();
            args.Append($"-s {device.Id} shell CLASSPATH={kServerDevicePath} app_process / {kServerMainClass}");
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

                    // Allocated per frame because Texture2D.LoadImage takes a whole
                    // array with no length, so the buffer has to be exactly one frame.
                    var payload = new byte[size];
                    ReadExactly(stream, payload, size);

                    lock (m_FrameLock)
                    {
                        // Only the newest frame is kept: if the Editor cannot keep up,
                        // showing the latest screen matters more than showing every
                        // frame.
                        m_PendingFrame = payload;
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
            if (m_Errors.Length > 0)
            {
                EditorGUI.HelpBox(rc, m_Errors.ToString(), MessageType.Error);
                return;
            }

            if (m_Texture == null)
            {
                var message = IsStreaming
                    ? "Starting the stream on the device..."
                    : "Not streaming, click Start.";
                EditorGUI.HelpBox(rc, message, MessageType.Info);
                return;
            }

            GUI.DrawTexture(rc, m_Texture, ScaleMode.ScaleToFit);

            if (IsStreaming)
            {
                var label = $"{m_FrameWidth}x{m_FrameHeight}   {m_Fps:0.0} fps   {m_Mbps:0.00} Mbps";
                GUI.Label(new Rect(rc.x + 4, rc.y + 2, rc.width - 8, EditorGUIUtility.singleLineHeight), label);
            }
        }

        internal void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing live stream details:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Socket", string.IsNullOrEmpty(m_SocketName) ? "-" : m_SocketName);
            EditorGUILayout.LabelField("Forwarded port", m_ForwardedPort > 0 ? m_ForwardedPort.ToString() : "-");
            EditorGUILayout.LabelField("Server on device", kServerDevicePath);

            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            if (GUILayout.Button("Log server output", AndroidLogcatStyles.toolbarButton))
            {
                string log;
                lock (m_ServerLog)
                    log = m_ServerLog.ToString();
                UnityEngine.Debug.Log(string.IsNullOrEmpty(log) ? "No server output captured" : log);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
