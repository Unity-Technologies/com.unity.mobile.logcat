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

        internal class StreamingData
        {
            byte[] m_BackBuffer;
            byte[] m_FontBuffer;
            int m_LeftToRead;
            int m_SwapIdx;
            int m_TextureApplyIdx;
            Texture2D m_Texture;

            internal Texture2D Texture => m_Texture;

            internal StreamingData(int width, int height)
            {
                m_Texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var dataSize = width * height * 3;
                m_BackBuffer = new byte[dataSize];
                m_FontBuffer = new byte[dataSize];
                m_LeftToRead = dataSize;
                m_SwapIdx = 0;
                m_TextureApplyIdx = 0;
            }

            /// <summary>
            /// Writes data into back buffer, called from worker thread
            /// </summary>
            /// <returns>Returns true if back buffer is full and we need to swap.</returns>
            internal int WriteDataToBackBuffer(FileStream baseStream)
            {
                if (m_LeftToRead <= 0)
                    throw new Exception("No more room in buffer to write, did you forget to swap buffers?");

                int bytesRead = 0;
                lock (m_BackBuffer)
                    bytesRead = baseStream.Read(m_BackBuffer, m_BackBuffer.Length - m_LeftToRead, m_LeftToRead);

                m_LeftToRead -= bytesRead;
                if (m_LeftToRead <= 0)
                {
                    lock (m_BackBuffer)
                    {
                        var tmp = m_BackBuffer;
                        m_BackBuffer = m_FontBuffer;
                        m_FontBuffer = tmp;
                        m_LeftToRead = tmp.Length;
                    }
                    m_SwapIdx++;
                }

                return bytesRead;
            }

            internal void ApplyDataToTextureIfNeeded()
            {
                if (m_TextureApplyIdx < m_SwapIdx)
                {
                    lock (m_FontBuffer)
                        m_Texture.LoadRawTextureData(m_FontBuffer);
                    m_Texture.Apply();
                    m_TextureApplyIdx = m_SwapIdx;
                }
            }
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private Process m_StreamingProcess;
        private StringBuilder m_StreamingProcessErrors;
        private IAndroidLogcatDevice m_StreamingFromDevice;
        private DateTime m_StreamingCheckTime;
        private Action<Result> m_OnStopLiveStream;
        internal string Errors => m_StreamingProcessErrors != null ? m_StreamingProcessErrors.ToString() : string.Empty;
        StreamingData m_Data;

        Thread m_Thread;

        const int Width = 1600 / 6;
        const int Height = 2560 / 6;

        internal Texture2D Texture => m_Data != null ? m_Data.Texture : null;

        internal AndroidLogcatLiveStream(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            m_Runtime.Update += Update;
            m_Runtime.Closing += Cleanup;
        }

        private void Cleanup()
        {
            if (m_StreamingFromDevice == null || m_Runtime == null)
                return;
            // Cache, since StopRecording will clear m_RecordingOnDevice
            var device = m_StreamingFromDevice;
            StopStreaming();
            AndroidLogcatUtilities.KillScreenRecordProcess(m_Runtime, device);
            m_Runtime = null;
        }

        private void Update()
        {
            if (!IsStreaming)
                return;

            var currentTime = DateTime.Now;
            if ((currentTime - m_StreamingCheckTime).TotalSeconds > 1.0f)
            {
                m_StreamingCheckTime = currentTime;
                if (m_StreamingProcess.HasExited)
                {
                    var result = Result.Success;

                    var title = $"Process 'adb {m_StreamingProcess.StartInfo.Arguments}' has exited with code {m_StreamingProcess.ExitCode}.";

                    if (result == Result.Failure)
                    {
                        m_StreamingProcessErrors.AppendLine(title);
                        m_StreamingProcessErrors.AppendLine();
                    }
                    AndroidLogcatInternalLog.Log(title);

                    m_OnStopLiveStream?.Invoke(result);
                    ClearStreamingData();
                }
            }

            if (m_Data != null)
                m_Data.ApplyDataToTextureIfNeeded();
        }

        internal bool IsRemoteRecorderActive(IAndroidLogcatDevice device)
        {
            return AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, "screenrecord") != -1;
        }

        internal bool IsStreaming => m_StreamingProcess != null;

        internal void StartStreaming(IAndroidLogcatDevice device,
            Action<Result> onStopLiveStream,
            TimeSpan? timeLimit = null,
            uint? videoSizeX = null,
            uint? videoSizeY = null,
            ulong? bitRate = null,
            string displayId = null)
        {
            if (device == null)
                throw new InvalidOperationException("No device selected");

            if (m_StreamingProcess != null)
                throw new InvalidOperationException("Already recording");

            m_OnStopLiveStream = onStopLiveStream;
            m_StreamingFromDevice = device;

            AndroidLogcatUtilities.KillScreenRecordProcess(m_Runtime, m_StreamingFromDevice);

            // If for some reason screen recorder is still running, abort.
            if (IsRemoteRecorderActive(m_StreamingFromDevice))
            {
                m_StreamingFromDevice = null;
                throw new InvalidOperationException("screenrecord is already recording on the device, aborting...");
            }


            //var args = $"-s {m_RecordingOnDevice.Id} shell screenrecord";
            var args = $"-s {m_StreamingFromDevice.Id} exec-out screenrecord";
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

            m_StreamingProcessErrors = new StringBuilder();

            m_StreamingProcess = new Process();
            var si = m_StreamingProcess.StartInfo;
            si.FileName = m_Runtime.Tools.ADB.GetADBPath();
            si.Arguments = args;
            si.RedirectStandardError = true;
            si.RedirectStandardOutput = true;
            si.RedirectStandardInput = true;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            //m_RecordingProcess.OutputDataReceived += OutputDataReceived;
            // m_RecordingProcess.ErrorDataReceived += OutputDataReceived;
            m_StreamingProcess.Start();

            //m_RecordingProcess.BeginOutputReadLine();
            //m_RecordingProcess.BeginErrorReadLine();

            m_StreamingCheckTime = DateTime.Now;

            // We get data in RGB24 format
            m_Data = new StreamingData(Width, Height);
            m_Thread = new Thread(ConsumeStreamingData);
            m_Thread.Start(m_StreamingProcess);
        }

        private void ConsumeStreamingData(object p)
        {
            var process = (Process)p;
            FileStream baseStream = process.StandardOutput.BaseStream as FileStream;
            int lastRead = 0;


            do
            {
                lastRead = m_Data.WriteDataToBackBuffer(baseStream);
            } while (lastRead > 0);

            // TODO
            UnityEngine.Debug.Log("Exited");
        }

        internal bool StopStreaming()
        {
            if (m_StreamingProcess == null)
                return false;

            var result = Result.Success;
            try
            {
                m_StreamingProcess.Kill();
                m_StreamingProcess.WaitForExit();
                m_StreamingProcess.Close();

            }
            catch (Exception ex)
            {
                result = Result.Failure;
                m_StreamingProcessErrors.AppendLine("Failed to stop the recording");
                m_StreamingProcessErrors.AppendLine(ex.Message);
            }
            finally
            {
                m_OnStopLiveStream?.Invoke(result);
                ClearStreamingData();
            }

            return result == Result.Success;
        }

        private void ClearStreamingData()
        {
            m_StreamingProcess = null;
            m_StreamingFromDevice = null;
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

        public Rect GetVideoRect(Rect windowRect)
        {

            float aspect = (float)Width / (float)Height;
            var rc = GUILayoutUtility.GetAspectRect(aspect);
            Rect r1, r2;

            var correctedHeight = windowRect.height - rc.y - 20;
            var s = correctedHeight / rc.height;
            r1 = rc;
            r1.width *= s;
            r1.height *= s;

            var correctedWidth = windowRect.width - rc.x;
            s = correctedWidth / rc.width;
            r2 = rc;
            r2.width *= s;
            r2.height *= s;

            var videoRect = r1.width < r2.width ? r1 : r2;

            videoRect.x += (windowRect.width - videoRect.width) * 0.5f;

            return videoRect;
        }


        public void DoGUI(Rect rc)
        {
            if (Texture != null)
            {
                rc = GetVideoRect(rc);
                rc.y += rc.height;
                rc.height = -rc.height;
                GUI.DrawTexture(rc, Texture);
            }
            else
            {
                EditorGUILayout.HelpBox("No screenshot to show, click Capture button.", MessageType.Info);
            }
        }

    }
}
