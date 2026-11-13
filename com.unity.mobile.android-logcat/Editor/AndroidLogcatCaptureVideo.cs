using System;
using System.Collections.Generic;
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
        // Reads logcat for the recording, when it saves messages.
        AndroidLogcat m_Logcat;
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
                stream.DisplayRotation, stream.DisplaySize, stream.QuarterTurnsFor,
                m_Runtime.Settings.RecordingSquareVideo ? stream.LongestFrameSide() : 0,
                stream.ShownRealtimeUs, stream.ShownArrivalTicks);
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
            AndroidLogcatCaptureInfo.Create(device)?.Save(RecordingPath);
            m_OnStopRecording = onStopRecording;
            m_Clock.Restart();
            stream.Recorder = m_Recorder;
            if (m_Runtime.Settings.RecordLogcatMessages)
                StartLogcat(device, recorder);
        }

        /// <summary>
        /// A logcat of the recording's own, filtered the way the Android Logcat window is
        /// now, so the recording keeps what that window would show.
        /// </summary>
        void StartLogcat(IAndroidLogcatDevice device, AndroidLogcatStreamRecorder recorder)
        {
            if (m_Runtime.Tools == null)
                return;
            var user = m_Runtime.UserSettings;
            var process = user.LastSelectedProcess;
            var pid = process != null && process.deviceId == device.Id ? process.processId : 0;
            m_Logcat = new AndroidLogcat(m_Runtime, m_Runtime.Tools.ADB, device, pid, user.SelectedPriority,
                user.FilterOptions, user.Tags.GetSelectedTags(), forRecording: true);
            m_Logcat.FilteredLogEntriesAdded += recorder.NoteLogs;
            m_Logcat.Start();
        }

        void StopLogcat()
        {
            if (m_Logcat == null)
                return;
            m_Logcat.Flush();
            m_Logcat.Stop();
            m_Logcat = null;
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
            // Before the recorder, which places the messages when it stops.
            StopLogcat();

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
            else
            {
                SaveFrameChanges(path, recorder.FrameChanges);
            }
            m_Runtime.CaptureScreenshot.ReleaseCapturePath(path);

            onStopRecording?.Invoke(recorded ? Result.Success : Result.Failure, path);
            return recorded;
        }

        /// <summary>
        /// Folding or turning the device during a recording changes its display, so the
        /// details saved at the start get what changed over time.
        /// </summary>
        static void SaveFrameChanges(string path, IReadOnlyList<AndroidLogcatCaptureInfo.FrameChange> changes)
        {
            if (changes.Count == 0)
                return;
            var info = AndroidLogcatCaptureInfo.Load(path);
            if (info == null)
                return;
            var first = changes[0].display;
            if (first.IsSet)
            {
                info.displayWidth = first.width;
                info.displayHeight = first.height;
            }
            info.frameChanges = new AndroidLogcatCaptureInfo.FrameChange[changes.Count];
            for (var i = 0; i < changes.Count; i++)
                info.frameChanges[i] = changes[i];
            info.Save(path);
        }

        static void DeleteRecording(string path)
        {
            AndroidLogcatCaptureInfo.Delete(path);
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
