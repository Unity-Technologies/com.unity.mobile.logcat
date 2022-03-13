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
        internal static readonly string VideoPathOnDevice = "/sdcard/logcat_video.mp4";
        internal static readonly string VideoPathOnHost = Path.Combine(Application.dataPath, "..", "Temp", "logcat_video.mp4").Replace("\\", "/");
        private AndroidLogcatRuntimeBase m_Runtime;
        private Process m_RecordingProcess;
        private StringBuilder m_RecordingProcessLog;
        private StringBuilder m_RecordingProcessErrors;
        private IAndroidLogcatDevice m_RecordingOnDevice;
        private DateTime m_RecordingCheckTime;
        internal string Errors => m_RecordingProcessErrors != null ? m_RecordingProcessErrors.ToString() : string.Empty;
        internal string VideoPath => VideoPathOnHost;

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
            DeleteVideoOnDevice(device);
            DeleteVideoOnHost();
            KillRemoteRecorder(device);
            m_Runtime = null;
        }

        private void Update()
        {
            if (!IsRecording)
                return;

            var currentTime = DateTime.Now;
            if ((currentTime - m_RecordingCheckTime).TotalSeconds > 1.0f)
            {
                m_RecordingCheckTime = currentTime;
                if (m_RecordingProcess.HasExited)
                {
                    m_RecordingProcessErrors.AppendLine($"Process 'adb {m_RecordingProcess.StartInfo.Arguments}' has exited with code {m_RecordingProcess.ExitCode}.");
                    m_RecordingProcessErrors.AppendLine();
                    m_RecordingProcessErrors.AppendLine(m_RecordingProcessLog.ToString());
                    ClearRecordingData();
                }
            }
        }

        internal bool IsRemoteRecorderActive(IAndroidLogcatDevice device)
        {
            return AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, "screenrecord") != -1;
        }

        private void KillRemoteRecorder(IAndroidLogcatDevice device)
        {
            if (device == null)
                return;
            var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, "screenrecord");
            if (pid != -1)
                AndroidLogcatUtilities.KillProcesss(m_Runtime.Tools.ADB, device, pid);
        }

        private void DeleteVideoOnHost()
        {
            try
            {
                if (File.Exists(VideoPathOnHost))
                    File.Delete(VideoPathOnHost);
            }
            catch (Exception ex)
            {
                var msg = $"Failed to delete {VideoPathOnHost}\n{ex.Message}";
                UnityEngine.Debug.LogWarning(msg);
                AndroidLogcatInternalLog.Log(msg);
            }
        }

        private void DeleteVideoOnDevice(IAndroidLogcatDevice device)
        {
            try
            {
                m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {device.Id}",
                    $"shell rm {VideoPathOnDevice}"
                }, "Failed to delete");
            }
            catch
            {
                // ignored
            }
        }

        internal bool IsRecording => m_RecordingProcess != null;

        internal void StartRecording(IAndroidLogcatDevice device,
            uint? videoSizeX = null,
            uint? videoSizeY = null,
            ulong? bitRate = null,
            string displayId = null)
        {
            if (device == null)
                throw new Exception("No device selected");

            if (m_RecordingProcess != null)
                throw new Exception("Already recording");

            m_RecordingOnDevice = device;

            DeleteVideoOnHost();
            KillRemoteRecorder(m_RecordingOnDevice);

            // If for some reason screen recorder is still running, abort.
            if (IsRemoteRecorderActive(m_RecordingOnDevice))
            {
                m_RecordingOnDevice = null;
                throw new Exception("screenrecord is already recording on the device, aborting...");
            }

            DeleteVideoOnDevice(m_RecordingOnDevice);

            var args = $"-s {m_RecordingOnDevice.Id} shell screenrecord";
            if (videoSizeX != null && videoSizeY != null)
                args += $" --size {videoSizeX}x{videoSizeY}";
            if (bitRate != null)
                args += $" --bit-rate {bitRate}";
            if (displayId != null)
                args += $" --display-id {displayId}";
            args += $" {VideoPathOnDevice}";

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

            m_RecordingCheckTime = DateTime.Now;
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

                if (!CopyVideoFromDevice(m_RecordingOnDevice))
                    KillRemoteRecorder(m_RecordingOnDevice);

                DeleteVideoOnDevice(m_RecordingOnDevice);
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

        private bool CopyVideoFromDevice(IAndroidLogcatDevice device)
        {
            if (device == null)
                return false;

            try
            {
                // Need to wait for Android Screen recording to finish up
                // Otherwise the video will be incomplete
                while (true)
                {
                    if (!IsRemoteRecorderActive(device))
                        break;
                    if (EditorUtility.DisplayCancelableProgressBar("Waiting for recording to finish", "Waiting for 'screenrecord' process to quit", 0.3f))
                    {
                        EditorUtility.ClearProgressBar();
                        return false;
                    }
                    Thread.Sleep(500);
                }

                // Give it a second to settle down
                Thread.Sleep(1000);

                EditorUtility.DisplayProgressBar("Acquiring recording", $"Copy {VideoPathOnDevice} -> Temp/{Path.GetFileName(VideoPathOnHost)}", 0.6f);

                var msg = m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {m_RecordingOnDevice.Id}",
                    $"pull {VideoPathOnDevice} \"{VideoPathOnHost}\""
                }, "Failed to copy");
                AndroidLogcatInternalLog.Log(msg);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return true;
        }

        internal void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing debugging buttons:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            if (GUILayout.Button("Delete recording on device", AndroidLogcatStyles.toolbarButton))
            {
                DeleteVideoOnDevice(m_RecordingOnDevice);
            }
            if (GUILayout.Button("Delete recording on host", AndroidLogcatStyles.toolbarButton))
            {
                DeleteVideoOnHost();
            }
            if (GUILayout.Button("Get screen record pid", AndroidLogcatStyles.toolbarButton))
            {
                var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, m_Runtime.DeviceQuery.SelectedDevice, "screenrecord");
                UnityEngine.Debug.Log("screen record pid is " + pid);
            }

            if (GUILayout.Button("Copy Recording from device", AndroidLogcatStyles.toolbarButton))
                CopyVideoFromDevice(m_RecordingOnDevice);


            EditorGUILayout.EndHorizontal();
        }

    }
}
