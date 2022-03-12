using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCaptureVideo
    {
        private readonly string kVideoPathOnDevice = "/sdcard/logcat_video.mp4";
        private string VideoTempPath => Path.Combine(Application.dataPath, "..", "Temp", "logcat_video.mp4").Replace("\\", "/");
        private AndroidLogcatRuntimeBase m_Runtime;
        private Process m_RecordingProcess;
        private StringBuilder m_RecordingProcessLog;
        private StringBuilder m_RecordingProcessErrors;
        private IAndroidLogcatDevice m_RecordingOnDevice;
        private double m_RecordingCheckTime;
        internal string Errors => m_RecordingProcessErrors != null ? m_RecordingProcessErrors.ToString() : string.Empty;
        internal IAndroidLogcatDevice RecordingOnDevice => m_RecordingOnDevice;
        internal string VideoPath => VideoTempPath;

        internal AndroidLogcatCaptureVideo(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            m_Runtime.Update += Update;
            m_Runtime.Closing += Cleanup;
        }

        private void Cleanup()
        {
            if (m_RecordingOnDevice == null || m_Runtime == null)
                return;
            // Cache, since StopRecording will clear m_RecordingOnDevice
            var device = m_RecordingOnDevice;
            StopRecording();
            DeleteRecordingOnDevice(device);
            DeleteTempVideo();
            KillScreenRecorderProcessOnDevice(device);
            m_Runtime = null;
        }

        private void Update()
        {
            if (!IsRecording())
                return;

            var currentTime = Time.realtimeSinceStartup;
            if (currentTime - m_RecordingCheckTime > 1.0f)
            {
                m_RecordingCheckTime = currentTime;
                if (m_RecordingProcess.HasExited)
                {
                    m_RecordingProcessErrors.AppendLine($"Process 'adb shell screenrecord' has exited with code {m_RecordingProcess.ExitCode}.");
                    m_RecordingProcessErrors.AppendLine();
                    m_RecordingProcessErrors.AppendLine(m_RecordingProcessLog.ToString());
                    ClearRecordingData();

                    UnityEngine.Debug.Log("Check");
                }
            }
        }

        internal bool IsAndroidScreenRecordingProcessActive(IAndroidLogcatDevice device)
        {
            return AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, "screenrecord") != -1;
        }

        private void KillScreenRecorderProcessOnDevice(IAndroidLogcatDevice device)
        {
            var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, "screenrecord");
            if (pid != -1)
                AndroidLogcatUtilities.KillProcesss(m_Runtime.Tools.ADB, device, pid);
        }

        internal void DeleteTempVideo()
        {
            if (File.Exists(VideoTempPath))
                File.Delete(VideoTempPath);
        }

        internal void DeleteRecordingOnDevice(IAndroidLogcatDevice device)
        {
            try
            {
                m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {device.Id}",
                    $"shell rm {kVideoPathOnDevice}"
                }, "Failed to delete");
            }
            catch
            {
                // ignored
            }
        }

        internal bool IsRecording()
        {
            return m_RecordingProcess != null;
        }

        internal void StartRecording(IAndroidLogcatDevice device)
        {
            if (device == null)
                throw new Exception("No device selected");

            if (m_RecordingProcess != null)
                throw new Exception("Already recording");

            m_RecordingOnDevice = device;

            KillScreenRecorderProcessOnDevice(m_RecordingOnDevice);

            // If for some reason screen recorder is still running, abort.
            if (IsAndroidScreenRecordingProcessActive(m_RecordingOnDevice))
            {
                m_RecordingOnDevice = null;
                throw new Exception("Android is already recording");
            }

            DeleteRecordingOnDevice(m_RecordingOnDevice);

            var args = $"-s {m_RecordingOnDevice.Id} shell screenrecord";
            var rs = m_Runtime.UserSettings.RecorderSettings;
            if (rs.VideoSizeEnabled)
                args += $" --size {rs.VideoSizeX}x{rs.VideoSizeY}";
            if (rs.BitRateEnabled)
                args += $" --bit-rate {rs.BitRate}";
            if (rs.DisplayIdEnabled)
                args += $" --display-id {rs.DisplayId}";
            args += $" {kVideoPathOnDevice}";

            AndroidLogcatInternalLog.Log($"{m_Runtime.Tools.ADB.GetADBPath()} {args}");

            m_RecordingProcessLog = new StringBuilder();
            m_RecordingProcessErrors = new StringBuilder();

            m_RecordingProcess = new Process();
            var si = m_RecordingProcess.StartInfo;
            si.FileName = m_Runtime.Tools.ADB.GetADBPath();
            si.Arguments = args;
            si.RedirectStandardError = true;
            si.RedirectStandardOutput = true;
            si.RedirectStandardInput = true;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            m_RecordingProcess.OutputDataReceived += OutputDataReceived;
            m_RecordingProcess.ErrorDataReceived += OutputDataReceived;
            m_RecordingProcess.Start();

            m_RecordingProcess.BeginOutputReadLine();
            m_RecordingProcess.BeginErrorReadLine();
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            m_RecordingProcessLog.AppendLine(e.Data);
        }

        internal bool StopRecording()
        {
            if (m_RecordingProcess == null)
                return false;
            try
            {
                m_RecordingProcess.Kill();
                m_RecordingProcess.WaitForExit();
                m_RecordingProcess.Close();
            }
            finally
            {
                ClearRecordingData();
            }
            return true;
        }

        private void ClearRecordingData()
        {
            m_RecordingProcess = null;
            m_RecordingOnDevice = null;
        }

        internal void CopyRecordingFromDevice(IAndroidLogcatDevice device)
        {
            try
            {
                // Need to wait for Android Screen recording to finish up
                // Otherwise the video will be incomplete
                while (true)
                {
                    if (!IsAndroidScreenRecordingProcessActive(device))
                        break;
                    if (EditorUtility.DisplayCancelableProgressBar("Waiting for recording to finish", "Waiting for 'screenrecord' process to quit", 0.3f))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                    Thread.Sleep(500);
                }

                // Give it a second to settle down
                Thread.Sleep(1000);

                EditorUtility.DisplayProgressBar("Acquiring recording", $"Copy {kVideoPathOnDevice} -> Temp/{Path.GetFileName(VideoTempPath)}", 0.6f);

                var msg = m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {device.Id}",
                    $"pull {kVideoPathOnDevice} \"{VideoTempPath}\""
                }, "Failed to copy");
                //m_Log.AppendLine(msg);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
