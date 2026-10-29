using System;
using System.IO;

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
        DateTime m_Started;

        /// <summary>Why the last recording failed, empty when it did not.</summary>
        internal string Errors { get; private set; } = string.Empty;

        /// <summary>The recording being taken, or the last one. Empty before the first.</summary>
        internal string RecordingPath { get; private set; } = string.Empty;

        internal bool IsRecording => m_Recorder != null;

        internal TimeSpan Elapsed => IsRecording ? DateTime.Now - m_Started : TimeSpan.Zero;

        internal AndroidLogcatCaptureVideo(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            m_Runtime.Update += Update;
            m_Runtime.Closing += () => StopRecording();
        }

        void Update()
        {
            // Nothing more will arrive: a capture was selected, or the stream failed.
            if (IsRecording && !m_Runtime.LiveStream.IsStreaming)
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
            RecordingPath = captures.AllocateCapturePath(device, captures.GetVideoExtension());
            try
            {
                m_Recorder = new AndroidLogcatStreamRecorder(RecordingPath, m_Runtime.Settings.LiveStreamMaxFps, stream.Texture,
                    stream.ViewQuarterTurns, stream.QuarterTurnsFor);
            }
            catch
            {
                captures.ReleaseCapturePath(RecordingPath);
                throw;
            }
            // The same details a screenshot is saved with, so the list can say which device it came from.
            AndroidLogcatScreenshotInfo.Create(device)?.Save(RecordingPath);
            m_OnStopRecording = onStopRecording;
            m_Started = DateTime.Now;
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
            if (!recorded)
            {
                Errors = string.IsNullOrEmpty(recorder.Error) ? "No frames were recorded." : recorder.Error;
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
            m_Runtime.CaptureScreenshot.ReleaseCapturePath(path);

            onStopRecording?.Invoke(recorded ? Result.Success : Result.Failure, path);
            return recorded;
        }
    }
}
