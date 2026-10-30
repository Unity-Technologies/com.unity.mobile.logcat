using System;
using System.IO;
using UnityEditor;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Records the live stream into a capture, through <see cref="AndroidLogcatStreamRecorder"/>.
    /// A recording needs the stream running, and ends when the stream does.
    /// </summary>
    internal class AndroidLogcatCaptureVideo
    {
        internal enum Result
        {
            Success,
            Failure
        }

        readonly AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatStreamRecorder m_Recorder;
        Action<Result, string> m_OnStopRecording;
        // Not the wall clock, which a clock change would move the time limit with.
        readonly System.Diagnostics.Stopwatch m_Clock = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Why the last recording failed or stopped early, empty when it did not. One that
        /// stopped early is still kept, with what was written before.
        /// </summary>
        internal string Errors { get; private set; } = string.Empty;

        /// <summary>The recording being taken, or the last one. Empty before the first.</summary>
        internal string RecordingPath { get; private set; } = string.Empty;

        internal bool IsRecording => m_Recorder != null;

        internal TimeSpan Elapsed => IsRecording ? m_Clock.Elapsed : TimeSpan.Zero;

        internal AndroidLogcatCaptureVideo(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            m_Runtime.Update += Update;
            m_Runtime.Closing += Close;
            // A reload ends the recorder without closing the file, and an MP4 is only
            // playable once its index is written on close - so it is saved first.
            AssemblyReloadEvents.beforeAssemblyReload += SaveBeforeReload;
        }

        void Close()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SaveBeforeReload;
            StopRecording();
        }

        void SaveBeforeReload()
        {
            StopRecording();
        }

        void Update()
        {
            // Nothing more will arrive: a capture was selected, or the stream failed.
            if (IsRecording && !m_Runtime.LiveStream.IsStreaming)
                StopRecording();
            if (IsRecording && Elapsed.TotalMinutes >= m_Runtime.Settings.RecordingTimeLimit)
                StopRecording();
            // The encoder failed, so nothing more will be written.
            if (IsRecording && !string.IsNullOrEmpty(m_Recorder.Error))
                StopRecording();
        }

        internal void StartRecording(IAndroidLogcatDevice device, Action<Result, string> onStopRecording)
        {
            if (device == null)
                throw new InvalidOperationException("No device selected");
            if (IsRecording)
                throw new InvalidOperationException("Already recording");
            var stream = m_Runtime.LiveStream;
            if (stream == null || !stream.IsStreaming)
                throw new InvalidOperationException("Recording needs the live stream");

            Errors = string.Empty;
            var captures = m_Runtime.CaptureScreenshot;
            // Numbered like a screenshot, and reserved the same way.
            var path = captures.AllocateCapturePath(device, captures.GetVideoExtension());
            // The constructor reports failures through Error rather than throwing.
            var recorder = new AndroidLogcatStreamRecorder(path, m_Runtime.Settings.LiveStreamMaxFps, stream.Texture,
                stream.ViewQuarterTurns, stream.QuarterTurnsFor);
            // The encoder is created from the live view's frame, and failing to is
            // reported here rather than as a recording that never writes anything.
            if (!string.IsNullOrEmpty(recorder.Error))
            {
                Errors = recorder.Error;
                recorder.Stop();
                AndroidLogcatInternalLog.Log(recorder.Stats());
                DeleteRecording(path);
                captures.ReleaseCapturePath(path);
                throw new InvalidOperationException(Errors);
            }
            RecordingPath = path;
            m_Recorder = recorder;
            // The same details a screenshot is saved with, so the list can say which device it came from.
            AndroidLogcatScreenshotInfo.Create(device)?.Save(RecordingPath);
            m_OnStopRecording = onStopRecording;
            m_Clock.Restart();
            stream.Recorder = m_Recorder;
        }

        /// <summary>Returns false when nothing was recording, or nothing was recorded.</summary>
        internal bool StopRecording()
        {
            var recorder = m_Recorder;
            if (recorder == null)
                return false;

            // Cleared before the callback, which can leave the live view and stop again.
            var onStopRecording = m_OnStopRecording;
            m_Recorder = null;
            m_OnStopRecording = null;
            var stream = m_Runtime.LiveStream;
            if (stream != null)
                stream.Recorder = null;

            var recorded = recorder.Stop();
            AndroidLogcatInternalLog.Log(recorder.Stats());
            var path = recorder.Path;
            Errors = recorder.Error;
            if (!recorded)
            {
                if (string.IsNullOrEmpty(Errors))
                    Errors = "No frames were recorded.";
                DeleteRecording(path);
            }
            m_Runtime.CaptureScreenshot.ReleaseCapturePath(path);

            onStopRecording?.Invoke(recorded ? Result.Success : Result.Failure, path);
            return recorded;
        }

        static void DeleteRecording(string path)
        {
            AndroidLogcatScreenshotInfo.Delete(path);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to delete '{path}': {ex.Message}");
            }
        }
    }
}
