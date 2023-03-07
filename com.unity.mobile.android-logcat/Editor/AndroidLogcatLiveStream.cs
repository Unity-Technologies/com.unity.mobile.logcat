using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatLiveStream
    {
        internal enum Result
        {
            Success,
            Failure
        }
        private AndroidLogcatRuntimeBase m_Runtime;
        private Process m_RecordingProcess;
        private StringBuilder m_RecordingProcessLog;
        private StringBuilder m_RecordingProcessErrors;
        private IAndroidLogcatDevice m_RecordingOnDevice;
        private DateTime m_RecordingCheckTime;
        private Action<Result> m_OnStopLiveStream;
        internal string Errors => m_RecordingProcessErrors != null ? m_RecordingProcessErrors.ToString() : string.Empty;
        private Texture2D m_Texture;
        private byte[] m_Buffer;
        private int m_ToRead;

        Thread m_Thread;

        const int Width = 1440 / 4;
        const int Height = 2880 / 4;

        internal Texture2D Texture { get; }

        internal AndroidLogcatLiveStream(AndroidLogcatRuntimeBase runtime)
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
                    var result = Result.Success;

                    var title = $"Process 'adb {m_RecordingProcess.StartInfo.Arguments}' has exited with code {m_RecordingProcess.ExitCode}.";

                    if (result == Result.Failure)
                    {
                        m_RecordingProcessErrors.AppendLine(title);
                        m_RecordingProcessErrors.AppendLine();
                        m_RecordingProcessErrors.AppendLine(m_RecordingProcessLog.ToString());
                    }
                    AndroidLogcatInternalLog.Log(title);
                    AndroidLogcatInternalLog.Log(m_RecordingProcessLog.ToString());

                    m_OnStopLiveStream?.Invoke(result);
                    ClearRecordingData();
                }
            }

            lock (m_Buffer)
            {
                m_Texture.LoadRawTextureData(m_Buffer);
            }

            m_Texture.Apply();
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

        internal bool IsRecording => m_RecordingProcess != null;

        internal void StartRecording(IAndroidLogcatDevice device,
            Action<Result> onStopLiveStream,
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

            m_OnStopLiveStream = onStopLiveStream;
            m_RecordingOnDevice = device;

            KillRemoteRecorder(m_Runtime, m_RecordingOnDevice);

            // If for some reason screen recorder is still running, abort.
            if (IsRemoteRecorderActive(m_RecordingOnDevice))
            {
                m_RecordingOnDevice = null;
                throw new InvalidOperationException("screenrecord is already recording on the device, aborting...");
            }


            //var args = $"-s {m_RecordingOnDevice.Id} shell screenrecord";
            var args = $"-s {m_RecordingOnDevice.Id} exec-out screenrecord";
            // TODO: remove timelimit?
            //if (timeLimit != null)
            //    args += $" --time-limit {((TimeSpan)timeLimit).TotalSeconds}";
            //if (videoSizeX != null && videoSizeY != null)
            //    args += $" --size {videoSizeX}x{videoSizeY}";
            if (bitRate != null)
                args += $" --bit-rate {bitRate}";
            if (displayId != null)
                args += $" --display-id {displayId}";

            args += $" --size {Width}x{Height}";
            args += " --output-format=raw-frames -";



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
            //m_RecordingProcess.OutputDataReceived += OutputDataReceived;
            // m_RecordingProcess.ErrorDataReceived += OutputDataReceived;
            m_RecordingProcess.Start();

            //m_RecordingProcess.BeginOutputReadLine();
            //m_RecordingProcess.BeginErrorReadLine();

            m_RecordingCheckTime = DateTime.Now;

            m_Texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            m_Buffer = new byte[Width * Height * 3];
            m_ToRead = m_Buffer.Length;

            m_Thread = new Thread(Test);
            m_Thread.Start(m_RecordingProcess);
        }

        private void Test(object p)
        {
            var process = (Process)p;
            FileStream baseStream = process.StandardOutput.BaseStream as FileStream;
            int lastRead = 0;


            do
            {
                lock (m_Buffer)
                {
                    lastRead = baseStream.Read(m_Buffer, m_Buffer.Length - m_ToRead, m_ToRead);
                }

                m_ToRead -= lastRead;
                if (m_ToRead <= 0)
                    m_ToRead = m_Buffer.Length;

                UnityEngine.Debug.Log($"Incoming {lastRead}, m_ToRead {m_ToRead}");
            } while (lastRead > 0);

            /*
                byte[] imageBytes = null;


            using (MemoryStream ms = new MemoryStream())
            {

                byte[] buffer = new byte[4096];
                do
                {
                    lastRead = baseStream.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, lastRead);

                    UnityEngine.Debug.Log($"Incoming {buffer.Length}");
                } while (lastRead > 0);

                imageBytes = ms.ToArray();
            }
            */
            UnityEngine.Debug.Log("Exited");
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            //m_RecordingProcessLog.AppendLine(e.Data);
            UnityEngine.Debug.Log($"Received {e.Data.Length}");
        }

        internal bool StopRecording()
        {
            if (m_RecordingProcess == null)
                return false;

            var result = Result.Success;
            try
            {
                m_RecordingProcess.Kill();
                m_RecordingProcess.WaitForExit();
                m_RecordingProcess.Close();

            }
            catch (Exception ex)
            {
                result = Result.Failure;
                m_RecordingProcessErrors.AppendLine("Failed to stop the recording");
                m_RecordingProcessErrors.AppendLine(ex.Message);
            }
            finally
            {
                m_OnStopLiveStream?.Invoke(result);
                ClearRecordingData();
            }

            return result == Result.Success;
        }

        private void ClearRecordingData()
        {
            m_RecordingProcess = null;
            m_RecordingOnDevice = null;
            m_OnStopLiveStream = null;
        }

        internal void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing debugging buttons:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            if (GUILayout.Button("Get screen record pid", AndroidLogcatStyles.toolbarButton))
            {
                var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, m_Runtime.DeviceQuery.SelectedDevice, "screenrecord");
                UnityEngine.Debug.Log("screen record pid is " + pid);
            }



            EditorGUILayout.EndHorizontal();
        }

        public void DoGUI(Rect rc)
        {
            if (m_Texture != null)
            {
                GUI.DrawTexture(rc, m_Texture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("No screenshot to show, click Capture button.", MessageType.Info);
            }
        }

    }
}
