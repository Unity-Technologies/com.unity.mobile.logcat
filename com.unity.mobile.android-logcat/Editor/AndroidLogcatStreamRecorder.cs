using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Media;
using Unity.Profiling;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Records the live stream's JPEG frames into an MP4 with MediaEncoder. Frames are
    /// queued from the reader thread and encoded on EditorApplication.update, whether
    /// or not the window paints.
    /// <para>
    /// Constant frame rate: the recording is cut into 1/frame rate slots and every slot
    /// gets exactly one frame. The device only sends a frame when the screen changes, so
    /// a slot gets the newest frame that arrived in it, or a repeat of the previous one.
    /// That keeps VideoPlayer's frame numbers and times exact, so the player can step and
    /// seek by frame without reading the file's timing.
    /// </para>
    /// </summary>
    [NoAutoStaticsCleanup]
    internal class AndroidLogcatStreamRecorder
    {
        const float kBitsPerPixel = 0.1f;
        // About three seconds. Past that the Editor is not keeping up, and frames are
        // dropped rather than piling up in memory.
        const int kMaxQueued = 90;
        // Per Editor update. The rest waits rather than stalling the Editor.
        const double kBudgetMs = 8;
        // How far behind now a still screen's repeats are written, so a frame still on
        // its way from the device can take its slot.
        const long kIdleMarginUs = 100000;

        // Found by typing "AndroidLogcat" into the Profiler's search box.
        static readonly ProfilerMarker k_Update = new ProfilerMarker("AndroidLogcat.RecordUpdate");
        static readonly ProfilerMarker k_Decode = new ProfilerMarker("AndroidLogcat.RecordDecode");
        static readonly ProfilerMarker k_Compose = new ProfilerMarker("AndroidLogcat.RecordCompose");
        static readonly ProfilerMarker k_Encode = new ProfilerMarker("AndroidLogcat.RecordEncode");

        struct QueuedFrame
        {
            internal byte[] Data;
            internal long PtsUs;
            internal long ArrivalUs;
            internal int Rotation;
        }

        readonly string m_Path;
        // Every frame lasts exactly 1/m_FrameRate s.
        readonly int m_FrameRate;
        readonly object m_Lock = new object();
        readonly Queue<QueuedFrame> m_Queue = new Queue<QueuedFrame>();
        // The recording's own time, from Start. The device only sends a frame when the
        // screen changes, so its timestamps alone cannot say when recording began.
        readonly Stopwatch m_Clock = Stopwatch.StartNew();
        // The live view's turn for a frame's display rotation, which the recording follows.
        readonly Func<int, int> m_QuarterTurnsFor;

        MediaEncoder m_Encoder;
        Texture2D m_Decoded;
        // The frame for slot m_Written, not written yet: a newer one may still replace it.
        Texture2D m_Canvas;
        bool m_HasPending;
        int m_Width;
        int m_Height;
        long m_Written;
        // Writes of the pending frame owed before the next one can take its place.
        long m_Owed;
        // Decoded, waiting for m_Owed to be paid before it becomes the pending frame.
        bool m_HasDecoded;
        int m_DecodedTurns;
        long m_FirstPtsUs = -1;
        // Where the first device frame lands on the recording's clock.
        long m_FirstArrivalUs;

        // Stats, logged when a recording stops.
        int m_Received;
        int m_Replaced;
        int m_Dropped;
        int m_Failed;
        double m_DecodeMs;
        double m_EncodeMs;
        double m_WorstUpdateMs;

        internal string Path => m_Path;
        internal string Error { get; private set; } = string.Empty;

        /// <summary>The newest frame decoded, which the live view shows instead of decoding it again.</summary>
        internal Texture2D Latest => m_Decoded;

        /// <summary>The device timestamp of <see cref="Latest"/>, -1 before the first.</summary>
        internal long LatestPtsUs { get; private set; } = -1;

        /// <param name="current">
        /// What the live view shows now, recorded as the first frame: a still screen
        /// sends nothing until it changes. Null before the stream's first frame.
        /// </param>
        /// <param name="currentTurns">The live view's turn of <paramref name="current"/>.</param>
        /// <param name="frameRate">The live stream's Max Frame Rate: the most it can deliver.</param>
        internal AndroidLogcatStreamRecorder(string path, int frameRate, Texture2D current, int currentTurns,
            Func<int, int> quarterTurnsFor)
        {
            m_Path = path;
            m_FrameRate = Math.Max(1, frameRate);
            m_QuarterTurnsFor = quarterTurnsFor;
            if (current != null)
            {
                try
                {
                    CreateEncoder(current.width, current.height, currentTurns);
                    using (k_Compose.Auto())
                        Compose(current, currentTurns);
                    m_HasPending = true;
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
            EditorApplication.update += Update;
        }

        long NowUs => m_Clock.ElapsedTicks * 1000000 / Stopwatch.Frequency;

        /// <summary>The slot a time falls in: frame number N covers N/m_FrameRate s.</summary>
        long SlotAt(long us) => (long)Math.Round(us * (double)m_FrameRate / 1000000);

        /// <summary>Reader thread. Copies the frame, since the reader reuses its buffers.</summary>
        internal void Enqueue(byte[] data, int size, long ptsUs, int rotation)
        {
            var copy = new byte[size];
            Buffer.BlockCopy(data, 0, copy, 0, size);
            lock (m_Lock)
            {
                if (m_Queue.Count >= kMaxQueued)
                {
                    m_Dropped++;
                    return;
                }
                m_Queue.Enqueue(new QueuedFrame { Data = copy, PtsUs = ptsUs, ArrivalUs = NowUs, Rotation = rotation });
            }
        }

        void Update()
        {
            using (k_Update.Auto())
                Drain(kBudgetMs);
        }

        void Drain(double budgetMs)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                while (string.IsNullOrEmpty(Error) && watch.Elapsed.TotalMilliseconds < budgetMs && Step())
                {
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            // Not the flush at Stop, which is allowed to take as long as it needs.
            if (budgetMs < double.MaxValue)
                m_WorstUpdateMs = Math.Max(m_WorstUpdateMs, watch.Elapsed.TotalMilliseconds);
        }

        /// <summary>One piece of work, in order: an owed write, a waiting frame, the next queued one.</summary>
        bool Step()
        {
            if (m_Owed > 0)
            {
                WritePending();
                m_Owed--;
                return true;
            }

            if (m_HasDecoded)
            {
                var watch = Stopwatch.StartNew();
                using (k_Compose.Auto())
                    Compose(m_Decoded, m_DecodedTurns);
                m_EncodeMs += watch.Elapsed.TotalMilliseconds;
                m_HasDecoded = false;
                m_HasPending = true;
                return true;
            }

            var frame = default(QueuedFrame);
            var idle = false;
            lock (m_Lock)
            {
                if (m_Queue.Count == 0)
                    idle = true;
                else
                    frame = m_Queue.Dequeue();
            }
            if (!idle)
            {
                Decode(frame);
                return true;
            }

            // A still screen sends nothing, so its repeats are written as time passes rather
            // than all at once when the next frame comes, or at Stop.
            if (m_HasPending && m_Written < SlotAt(NowUs - kIdleMarginUs))
            {
                var before = m_Written;
                WritePending();
                return m_Written > before;
            }
            return false;
        }

        void Decode(QueuedFrame frame)
        {
            var watch = Stopwatch.StartNew();
            if (m_Decoded == null)
                m_Decoded = new Texture2D(2, 2, TextureFormat.RGB24, true);
            bool decoded;
            using (k_Decode.Auto())
                decoded = ImageConversion.LoadImage(m_Decoded, new ReadOnlySpan<byte>(frame.Data));
            if (!decoded)
            {
                m_Failed++;
                return;
            }
            m_DecodeMs += watch.Elapsed.TotalMilliseconds;
            m_Received++;
            LatestPtsUs = frame.PtsUs;
            m_DecodedTurns = m_QuarterTurnsFor(frame.Rotation);

            if (m_Encoder == null)
                CreateEncoder(m_Decoded.width, m_Decoded.height, m_DecodedTurns);

            // Device spacing, anchored where the first device frame arrived, then rounded
            // to a slot - the only place a frame's time is decided.
            if (m_FirstPtsUs < 0)
            {
                m_FirstPtsUs = frame.PtsUs;
                m_FirstArrivalUs = frame.ArrivalUs;
            }
            var slot = SlotAt(m_FirstArrivalUs + frame.PtsUs - m_FirstPtsUs);

            // The pending frame fills every slot up to this one. In its own slot, or
            // an earlier one, this frame replaces it instead.
            if (m_HasPending && slot > m_Written)
                m_Owed = slot - m_Written;
            else if (m_HasPending)
                m_Replaced++;
            m_HasDecoded = true;
        }

        /// <summary>One slot. Written without a timestamp, so the encoder spaces it 1/m_FrameRate s on.</summary>
        void WritePending()
        {
            var watch = Stopwatch.StartNew();
            bool added;
            using (k_Encode.Auto())
                added = m_Encoder.AddFrame(m_Canvas);
            if (added)
                m_Written++;
            else
                m_Failed++;
            m_EncodeMs += watch.Elapsed.TotalMilliseconds;
        }

        void Fail(Exception ex)
        {
            Error = ex.Message;
            AndroidLogcatInternalLog.Log($"Stream recording failed: {ex}");
        }

        /// <summary>Sized to the first frame as the view shows it, turned or not.</summary>
        void CreateEncoder(int width, int height, int turns)
        {
            var sideways = (turns & 1) != 0;
            // H.264 needs even sizes.
            m_Width = (sideways ? height : width) & ~1;
            m_Height = (sideways ? width : height) & ~1;
            var h264 = new H264EncoderAttributes
            {
                // A keyframe a second; the frames between are differences, so a repeat is nearly free.
                gopSize = (uint)m_FrameRate,
                // Frames stored in the order they are shown. High profile reorders them
                // anyway on Windows, and VideoPlayer warns about the timestamps.
                numConsecutiveBFrames = 0,
                profile = VideoEncodingProfile.H264Baseline
            };
            var attributes = new VideoTrackEncoderAttributes(h264)
            {
                frameRate = new MediaRational(m_FrameRate),
                width = (uint)m_Width,
                height = (uint)m_Height,
                includeAlpha = false,
                targetBitRate = (uint)(m_Width * m_Height * m_FrameRate * kBitsPerPixel)
            };
            m_Encoder = new MediaEncoder(m_Path, attributes);
        }

        /// <summary>
        /// Converts a frame to RGBA32, which is all the encoder takes, turned as the live
        /// view turns it and at the encoder's fixed size - centered on black when the
        /// size differs, after a rotation or an odd one.
        /// </summary>
        void Compose(Texture2D source, int turns)
        {
            if (m_Canvas == null)
                m_Canvas = new Texture2D(m_Width, m_Height, TextureFormat.RGBA32, false);

            var sideways = (turns & 1) != 0;
            var turnedWidth = sideways ? source.height : source.width;
            var turnedHeight = sideways ? source.width : source.height;
            var scale = Mathf.Min((float)m_Width / turnedWidth, (float)m_Height / turnedHeight);

            var target = RenderTexture.GetTemporary(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, m_Width, m_Height, 0);
            // Counter-clockwise, as the live view turns it, about the canvas centre.
            var centre = new Vector3(m_Width * 0.5f, m_Height * 0.5f, 0);
            GL.MultMatrix(Matrix4x4.TRS(centre, Quaternion.Euler(0, 0, turns * -90f), new Vector3(scale, scale, 1)));
            Graphics.DrawTexture(new Rect(-source.width * 0.5f, -source.height * 0.5f, source.width, source.height), source);
            GL.PopMatrix();
            m_Canvas.ReadPixels(new Rect(0, 0, m_Width, m_Height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }

        /// <summary>
        /// Encodes what is still queued, holds the last frame until now, and closes the
        /// file. Returns false when nothing was written, which leaves no file behind.
        /// </summary>
        internal bool Stop()
        {
            // Taken first, so the time the final writes take does not lengthen the recording.
            var stopSlot = SlotAt(NowUs);
            EditorApplication.update -= Update;
            Drain(double.MaxValue);
            try
            {
                if (m_HasPending && string.IsNullOrEmpty(Error))
                {
                    var last = Math.Max(stopSlot, m_Written);
                    while (m_Written <= last && string.IsNullOrEmpty(Error))
                    {
                        var before = m_Written;
                        WritePending();
                        if (m_Written == before)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            m_Encoder?.Dispose();
            m_Encoder = null;
            if (m_Decoded != null)
                UnityEngine.Object.DestroyImmediate(m_Decoded);
            if (m_Canvas != null)
                UnityEngine.Object.DestroyImmediate(m_Canvas);
            m_Decoded = null;
            m_Canvas = null;
            return m_Written > 0 && string.IsNullOrEmpty(Error);
        }

        /// <summary>How the recording went, for the log.</summary>
        internal string Stats()
        {
            var seconds = m_Written / (double)m_FrameRate;
            return $"Stream recording: {m_Written} frames at {m_FrameRate} fps ({seconds:0.0} s), {m_Width}x{m_Height}, " +
                $"{m_Received} received, {m_Replaced} replaced by a newer one in the same slot, " +
                $"{m_Dropped} dropped (Editor behind), {m_Failed} failed. " +
                $"Per received frame: decode {m_DecodeMs / Math.Max(1, m_Received):0.0} ms. " +
                $"Per written frame: encode {m_EncodeMs / Math.Max(1, m_Written):0.0} ms. " +
                $"Worst update {m_WorstUpdateMs:0} ms.";
        }
    }
}
