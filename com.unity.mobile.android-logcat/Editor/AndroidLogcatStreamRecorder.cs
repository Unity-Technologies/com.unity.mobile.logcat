using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Prototype: records the live stream's JPEG frames into an MP4 with MediaEncoder,
    /// as an alternative to adb screenrecord. Frames are queued from the reader thread
    /// and encoded on EditorApplication.update, whether or not the window paints.
    /// </summary>
    internal class AndroidLogcatStreamRecorder
    {
        // About three seconds at 30 fps. Past that the Editor is not keeping up, and
        // frames are dropped rather than piling up in memory.
        const int kMaxQueued = 90;
        // Nominal only: every frame carries its own time, which makes the file VFR.
        const int kNominalFps = 30;

        struct QueuedFrame
        {
            internal byte[] Data;
            internal long PtsUs;
            internal long ArrivalUs;
        }

        readonly string m_Path;
        readonly object m_Lock = new object();
        readonly Queue<QueuedFrame> m_Queue = new Queue<QueuedFrame>();
        // The recording's own time, from Start. The device only sends a frame when the
        // screen changes, so its timestamps alone cannot say when recording began.
        readonly Stopwatch m_Clock = Stopwatch.StartNew();

        MediaEncoder m_Encoder;
        Texture2D m_Decoded;
        Texture2D m_Canvas;
        int m_Width;
        int m_Height;
        long m_FirstPtsUs = -1;
        // Where the first device frame lands on the recording's clock.
        long m_FirstArrivalUs;
        long m_LastPtsUs = -1;
        // The last frame added, repeated at Stop so a still screen keeps its length.
        Texture2D m_LastAdded;

        // Stats, for comparing against screenrecord.
        int m_Encoded;
        int m_Dropped;
        int m_Failed;
        int m_Composed;
        double m_DecodeMs;
        double m_EncodeMs;
        double m_WorstUpdateMs;

        internal string Path => m_Path;
        internal string Error { get; private set; } = string.Empty;
        internal int FramesEncoded => m_Encoded;

        /// <param name="current">
        /// What the live view shows now, recorded as the first frame: a still screen
        /// sends nothing until it changes. Null before the stream's first frame.
        /// </param>
        internal AndroidLogcatStreamRecorder(string path, Texture2D current)
        {
            m_Path = path;
            if (current != null)
            {
                try
                {
                    CreateEncoder(current.width, current.height);
                    Add(current, 0);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
            EditorApplication.update += Update;
        }

        long NowUs => m_Clock.ElapsedTicks * 1000000 / Stopwatch.Frequency;

        /// <summary>Reader thread. Copies the frame, since the reader reuses its buffers.</summary>
        internal void Enqueue(byte[] data, int size, long ptsUs)
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
                m_Queue.Enqueue(new QueuedFrame { Data = copy, PtsUs = ptsUs, ArrivalUs = NowUs });
            }
        }

        void Update()
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                QueuedFrame frame;
                lock (m_Lock)
                {
                    if (m_Queue.Count == 0)
                        break;
                    frame = m_Queue.Dequeue();
                }
                if (string.IsNullOrEmpty(Error))
                    Encode(frame);
            }
            m_WorstUpdateMs = Math.Max(m_WorstUpdateMs, watch.Elapsed.TotalMilliseconds);
        }

        void Encode(QueuedFrame frame)
        {
            try
            {
                var watch = Stopwatch.StartNew();
                if (m_Decoded == null)
                    m_Decoded = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!ImageConversion.LoadImage(m_Decoded, new ReadOnlySpan<byte>(frame.Data)))
                {
                    m_Failed++;
                    return;
                }
                m_DecodeMs += watch.Elapsed.TotalMilliseconds;

                if (m_Encoder == null)
                    CreateEncoder(m_Decoded.width, m_Decoded.height);

                // Device spacing, anchored where the first device frame arrived.
                if (m_FirstPtsUs < 0)
                {
                    m_FirstPtsUs = frame.PtsUs;
                    m_FirstArrivalUs = frame.ArrivalUs;
                }
                watch.Restart();
                Add(m_Decoded, m_FirstArrivalUs + frame.PtsUs - m_FirstPtsUs);
                m_EncodeMs += watch.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        /// <summary>Adds a frame at <paramref name="us"/>, nudged past the last one if need be.</summary>
        void Add(Texture2D texture, long us)
        {
            us = Math.Max(us, m_LastPtsUs + 1);
            var time = new MediaTime(us, 1000000, 1);

            // The encoder takes RGBA32 only, and a decoded JPEG is RGB24.
            bool added;
            if (texture.format == TextureFormat.RGBA32 && texture.width == m_Width && texture.height == m_Height)
            {
                added = m_Encoder.AddFrame(texture, time);
            }
            else
            {
                Compose(texture);
                m_Composed++;
                added = m_Encoder.AddFrame(m_Canvas, time);
            }

            if (added)
            {
                m_Encoded++;
                m_LastPtsUs = us;
                m_LastAdded = texture;
            }
            else
            {
                m_Failed++;
            }
        }

        void Fail(Exception ex)
        {
            Error = ex.Message;
            AndroidLogcatInternalLog.Log($"Stream recording failed: {ex}");
        }

        void CreateEncoder(int width, int height)
        {
            // H.264 needs even sizes.
            m_Width = width & ~1;
            m_Height = height & ~1;
            var attributes = new VideoTrackAttributes
            {
                frameRate = new MediaRational(kNominalFps),
                width = (uint)m_Width,
                height = (uint)m_Height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High
            };
            m_Encoder = new MediaEncoder(m_Path, attributes);
        }

        /// <summary>
        /// Converts a frame to RGBA32 at the encoder's fixed size, centered on black when
        /// its size differs - after a rotation, or an odd one.
        /// </summary>
        void Compose(Texture2D source)
        {
            if (m_Canvas == null)
                m_Canvas = new Texture2D(m_Width, m_Height, TextureFormat.RGBA32, false);

            var scale = Mathf.Min((float)m_Width / source.width, (float)m_Height / source.height);
            var w = source.width * scale;
            var h = source.height * scale;
            var rect = new Rect((m_Width - w) * 0.5f, (m_Height - h) * 0.5f, w, h);

            var target = RenderTexture.GetTemporary(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, m_Width, m_Height, 0);
            Graphics.DrawTexture(rect, source);
            GL.PopMatrix();
            m_Canvas.ReadPixels(new Rect(0, 0, m_Width, m_Height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }

        /// <summary>
        /// Encodes what is still queued and closes the file. Returns false when nothing
        /// was written, which leaves no file behind.
        /// </summary>
        internal bool Stop()
        {
            EditorApplication.update -= Update;
            Update();
            if (m_Encoder != null && m_LastAdded != null && string.IsNullOrEmpty(Error))
            {
                try
                {
                    Add(m_LastAdded, NowUs);
                    // The hold is the same frame, not a new one.
                    m_Encoded--;
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
            m_Encoder?.Dispose();
            m_Encoder = null;
            if (m_Decoded != null)
                UnityEngine.Object.DestroyImmediate(m_Decoded);
            if (m_Canvas != null)
                UnityEngine.Object.DestroyImmediate(m_Canvas);
            m_Decoded = null;
            m_Canvas = null;
            return m_Encoded > 0 && string.IsNullOrEmpty(Error);
        }

        /// <summary>What to compare against screenrecord.</summary>
        internal string Stats()
        {
            var frames = Math.Max(1, m_Encoded + m_Failed);
            var seconds = m_LastPtsUs > 0 ? m_LastPtsUs / 1000000.0 : 0;
            return $"Stream recording: {m_Encoded} frames over {seconds:0.0} s ({(seconds > 0 ? m_Encoded / seconds : 0):0.0} fps), " +
                $"{m_Width}x{m_Height}, {m_Dropped} dropped (Editor behind), {m_Failed} failed, {m_Composed} composed. " +
                $"Per frame: decode {m_DecodeMs / frames:0.0} ms, encode {m_EncodeMs / frames:0.0} ms. " +
                $"Worst update {m_WorstUpdateMs:0} ms.";
        }
    }
}
