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
        internal enum Result
        {
            Success,
            Failure
        }

        internal static readonly string VideoPathOnDevice = "/sdcard/logcat_video.mp4";
        private AndroidLogcatRuntimeBase m_Runtime;
        private Process m_RecordingProcess;
        private StringBuilder m_RecordingProcessLog;
        private StringBuilder m_RecordingProcessErrors;
        private IAndroidLogcatDevice m_RecordingOnDevice;
        private DateTime m_RecordingCheckTime;
        private Action<Result, string> m_OnStopRecording;
        internal string Errors => m_RecordingProcessErrors != null ? m_RecordingProcessErrors.ToString() : string.Empty;
        internal string GetVideoPath(IAndroidLogcatDevice device)
        {
            return AndroidLogcatUtilities.GetTemporaryPath(device, "video", ".mp4");
        }

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
            KillRemoteRecorder(m_Runtime, device);
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
                    var result = Result.Failure;
                    var targetPath = GetVideoPath(m_RecordingOnDevice);
                    // screenrecord has quit without errors, for ex., hit a time limit
                    // Note: On Google Pixel with Android 11 if screenrecord fails - our process exits with non zero exit code, but
                    //       On Cube U83 with Android 6 if screenrecord fails - process exits with 0 exit code, thus we additionally check if we were able to collect the recording
                    if (m_RecordingProcess.ExitCode == 0)
                    {
                        if (CollectRecording(targetPath))
                            result = Result.Success;

                    }

                    var title = $"Process 'adb {m_RecordingProcess.StartInfo.Arguments}' has exited with code {m_RecordingProcess.ExitCode}.";

                    if (result == Result.Failure)
                    {
                        m_RecordingProcessErrors.AppendLine(title);
                        m_RecordingProcessErrors.AppendLine();
                        m_RecordingProcessErrors.AppendLine(m_RecordingProcessLog.ToString());
                    }
                    AndroidLogcatInternalLog.Log(title);
                    AndroidLogcatInternalLog.Log(m_RecordingProcessLog.ToString());

                    m_OnStopRecording?.Invoke(result, targetPath);
                    ClearRecordingData();
                }
            }
        }

        internal bool IsRemoteRecorderActive(IAndroidLogcatDevice device)
        {
            return AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, "screenrecord") != -1;
        }

        internal static void KillRemoteRecorder(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device)
        {
            if (device == null)
                return;
            var pid = AndroidLogcatUtilities.GetPidFromPackageName(runtime.Tools.ADB, device, "screenrecord");
            if (pid != -1)
                AndroidLogcatUtilities.KillProcesss(runtime.Tools.ADB, device, pid);
        }

        private void DeleteVideoOnHost(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                var msg = $"Failed to delete {path}\n{ex.Message}";
                UnityEngine.Debug.LogWarning(msg);
                AndroidLogcatInternalLog.Log(msg);
            }
        }

        private void DeleteVideoOnDevice(IAndroidLogcatDevice device)
        {
            AndroidLogcatInternalLog.Log($"Clean '{VideoPathOnDevice}'");
            try
            {
                m_Runtime.Tools.ADB.Run(new[]
                {
                    $"-s {device.Id}",
                    $"shell rm -f {VideoPathOnDevice}"
                }, "Failed to delete");
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.ToString());
            }
        }

        internal bool IsRecording => m_RecordingProcess != null;

        internal void StartRecording(IAndroidLogcatDevice device,
            Action<Result, string> onStopRecording,
            TimeSpan? timeLimit = null,
            uint? videoSizeX = null,
            uint? videoSizeY = null,
            ulong? bitRate = null,
            string displayId = null)
        {
            if (device == null)
                throw new InvalidOperationException("No device selected");

            if (m_RecordingProcess != null)
                throw new InvalidOperationException("Already recording");

            m_OnStopRecording = onStopRecording;
            m_RecordingOnDevice = device;

            DeleteVideoOnHost(GetVideoPath(device));
            KillRemoteRecorder(m_Runtime, m_RecordingOnDevice);

            // If for some reason screen recorder is still running, abort.
            if (IsRemoteRecorderActive(m_RecordingOnDevice))
            {
                m_RecordingOnDevice = null;
                throw new InvalidOperationException("screenrecord is already recording on the device, aborting...");
            }

            DeleteVideoOnDevice(m_RecordingOnDevice);

            var args = $"-s {m_RecordingOnDevice.Id} shell screenrecord";
            if (timeLimit != null)
                args += $" --time-limit {((TimeSpan)timeLimit).TotalSeconds}";
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

            var targetPath = GetVideoPath(m_RecordingOnDevice);
            var result = Result.Success;
            try
            {
                m_RecordingProcess.Kill();
                m_RecordingProcess.WaitForExit();
                m_RecordingProcess.Close();

                if (!CollectRecording(targetPath))
                {
                    m_RecordingProcessErrors.AppendLine($"Failed to collect the recording '{VideoPathOnDevice}' -> '{targetPath}'");
                    result = Result.Failure;
                }
            }
            catch (Exception ex)
            {
                result = Result.Failure;
                m_RecordingProcessErrors.AppendLine("Failed to stop the recording");
                m_RecordingProcessErrors.AppendLine(ex.Message);
            }
            finally
            {
                m_OnStopRecording?.Invoke(result, targetPath);
                ClearRecordingData();
            }

            return result == Result.Success;
        }

        private bool CollectRecording(string targetPath)
        {
            var result = true;
            if (!CopyVideoFromDevice(m_RecordingOnDevice, targetPath))
            {
                result = false;
                KillRemoteRecorder(m_Runtime, m_RecordingOnDevice);
            }

            DeleteVideoOnDevice(m_RecordingOnDevice);
            return result;
        }

        private void ClearRecordingData()
        {
            m_RecordingProcess = null;
            m_RecordingOnDevice = null;
            m_OnStopRecording = null;
        }

        private bool CopyVideoFromDevice(IAndroidLogcatDevice device, string targetPath)
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
                    Thread.Sleep(100);
                }

                EditorUtility.DisplayProgressBar("Acquiring recording", $"Copy {VideoPathOnDevice} -> Temp/{Path.GetFileName(targetPath)}", 0.6f);

                try
                {
                    var msg = m_Runtime.Tools.ADB.Run(new[]
                    {
                        $"-s {m_RecordingOnDevice.Id}",
                        $"pull {VideoPathOnDevice} \"{targetPath}\""
                    }, "Failed to copy");
                    AndroidLogcatInternalLog.Log(msg);
                }
                catch (Exception ex)
                {
                    AndroidLogcatInternalLog.Log(ex.Message);
                    return false;
                }
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

            if (GUILayout.Button("Get screen record pid", AndroidLogcatStyles.toolbarButton))
            {
                var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, m_Runtime.DeviceQuery.SelectedDevice, "screenrecord");
                UnityEngine.Debug.Log("screen record pid is " + pid);
            }

            if (GUILayout.Button("Copy Recording from device", AndroidLogcatStyles.toolbarButton))
                CopyVideoFromDevice(m_RecordingOnDevice, GetVideoPath(m_RecordingOnDevice));


            EditorGUILayout.EndHorizontal();
        }

    }
}
