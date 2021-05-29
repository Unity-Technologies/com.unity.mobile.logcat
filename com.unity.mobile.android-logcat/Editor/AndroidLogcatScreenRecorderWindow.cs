using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine.Video;
using Debug = System.Diagnostics.Debug;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenRecorderWindow : AndroidLogcatToolsBaseWindow
    {
        private GUIContent kStartRecording = new GUIContent(L10n.Tr("Start Recording"));
        private GUIContent kStopRecording = new GUIContent(L10n.Tr("Stop Recording"));

        private readonly string kVideoPathOnDevice = "/sdcard/logcat_video.mp4";
        private string VideoTempPath => Path.Combine(Application.dataPath, "..", "Temp", "logcat_video.mp4").Replace("\\", "/");

        private Process m_RecordingProcess;
        private StringBuilder m_RecordingProcessLog;
        private StringBuilder m_RecordingProcessErrors;

        private VideoPlayer m_VideoPlayer;
        private long m_VideoFrame;

        public static void ShowWindow()
        {
            var win = EditorWindow.GetWindow<AndroidLogcatScreenRecorderWindow>("Device Video Capture");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_RecordingProcessLog = new StringBuilder();
            m_RecordingProcessErrors = new StringBuilder();
            m_VideoPlayer = AcquireVideoPlayer();
        }

        protected override void OnDisable()
        {
            UnityEngine.Debug.Log("Disable");
            StopRecording();
            DeleteRecordingOnDevice();
            DeleteTempVideo();
            base.OnDisable();
        }

        private VideoPlayer AcquireVideoPlayer()
        {
            var name = "LogcatVideoPlayer";
            var go = GameObject.Find(name);
            UnityEngine.Debug.Log(Application.isPlaying.ToString());
            if (go == null)
            {
                go = new GameObject(name);
                //TODO
                //go.hideFlags = HideFlags.HideAndDontSave;
                go.hideFlags = HideFlags.DontSave;
            }

            var player = go.GetComponent<VideoPlayer>();
            if (player == null)
                player = go.AddComponent<VideoPlayer>();
            player.renderMode = VideoRenderMode.APIOnly;
            player.isLooping = true;
            return player;
        }

        private void OnLostFocus()
        {
            UnityEngine.Debug.Log("FocusLost");
            m_VideoPlayer.Pause();
        }

        private bool IsAndroidScreenRecordingProcessActive()
        {
            return AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB,
                AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice, "screenrecord") != -1;
        }

        void DeleteRecordingOnDevice()
        {
            try
            {
                m_Runtime.Tools.ADB.Run(new[]
                {
                    $"shell rm {kVideoPathOnDevice}"
                }, "Failed to delete");
            }
            catch
            {
                // ignored
            }
        }

        void DeleteTempVideo()
        {
            if (File.Exists(VideoTempPath))
                File.Delete(VideoTempPath);
        }

        void CopyRecordingFromDevice()
        {
            try
            {
                // Need to wait for Android Screen recording to finish up
                // Otherwise the video will be incomplete
                while (true)
                {
                    if (!IsAndroidScreenRecordingProcessActive())
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
                    $"pull {kVideoPathOnDevice} \"{VideoTempPath}\""
                }, "Failed to copy");
                //m_Log.AppendLine(msg);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void StartRecording()
        {
            if (m_RecordingProcess != null)
                throw new Exception("Already recording");

            if (IsAndroidScreenRecordingProcessActive())
                throw new Exception("Android is already recording");

            DeleteRecordingOnDevice();

            // TODO: device id
            var args = "shell screenrecord";
            var rs = m_Runtime.UserSettings.RecorderSettings;
            if (rs.VideoSizeEnabled)
                args += $" --size {rs.VideoSizeX}x{rs.VideoSizeY}";
            if (rs.BitRateEnabled)
                args += $" --bit-rate {rs.BitRate}";
            if (rs.DisplayIdEnabled)
                args += $" --display-id {rs.DisplayId}";
            args += $" {kVideoPathOnDevice}";

            m_RecordingProcessLog = new StringBuilder();
            m_RecordingProcessErrors = new StringBuilder();

            m_RecordingProcess = new Process();
            m_RecordingProcess.StartInfo.FileName = m_Runtime.Tools.ADB.GetADBPath();
            m_RecordingProcess.StartInfo.Arguments = args;
            m_RecordingProcess.StartInfo.RedirectStandardError = true;
            m_RecordingProcess.StartInfo.RedirectStandardOutput = true;
            m_RecordingProcess.StartInfo.RedirectStandardInput = true;
            m_RecordingProcess.StartInfo.UseShellExecute = false;
            m_RecordingProcess.StartInfo.CreateNoWindow = true;
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

        bool StopRecording()
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
                m_RecordingProcess = null;
            }
            return true;
        }

        bool IsRecording()
        {
            return m_RecordingProcess != null;
        }

        void CheckRecordingForErrors()
        {
            if (!IsRecording())
                return;

            if (m_RecordingProcess.HasExited)
            {
                m_RecordingProcessErrors.AppendLine($"Process 'adb shell screenrecord' has exited with code {m_RecordingProcess.ExitCode}.");
                m_RecordingProcessErrors.AppendLine();
                m_RecordingProcessErrors.AppendLine(m_RecordingProcessLog.ToString());
                m_RecordingProcess = null;
            }
        }

        void DoRecordingGUI()
        {
            if (IsRecording())
            {
                if (GUILayout.Button(kStopRecording, AndroidLogcatStyles.toolbarButton))
                {
                    StopRecording();
                    CopyRecordingFromDevice();
                    DeleteRecordingOnDevice();

                    m_VideoPlayer.url = VideoTempPath;
                    m_VideoPlayer.Play();
                }
            }
            else
            {
                if (GUILayout.Button(kStartRecording, AndroidLogcatStyles.toolbarButton))
                {
                    DeleteTempVideo();
                    StartRecording();
                }
            }
        }

        void DoSaveGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(VideoTempPath));
            if (GUILayout.Button("Save...", AndroidLogcatStyles.toolbarButton, GUILayout.Width(kSaveButtonWidth)))
            {
                var path = EditorUtility.SaveFilePanel("Save Screen Capture", "", Path.GetFileName(VideoTempPath), Path.GetExtension(VideoTempPath));
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.Copy(VideoTempPath, path, true);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogErrorFormat("Failed to save to '{0}' as '{1}'.", path, ex.Message);
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        void DoRecordingSettingsGUI()
        {
            var rs = m_Runtime.UserSettings.RecorderSettings;
            var width = 100;
            EditorGUILayout.LabelField("Override Recording settings", EditorStyles.boldLabel);

            // Video Size
            EditorGUILayout.BeginHorizontal();
            rs.VideoSizeEnabled = GUILayout.Toggle(rs.VideoSizeEnabled, "Video Size", AndroidLogcatStyles.toolbarButton, GUILayout.MaxWidth(width));
            EditorGUI.BeginDisabledGroup(!rs.VideoSizeEnabled);
            rs.VideoSizeX = Math.Max(1, (uint)EditorGUILayout.IntField(GUIContent.none, (int)rs.VideoSizeX));
            rs.VideoSizeY = Math.Max(1, (uint)EditorGUILayout.IntField(GUIContent.none, (int)rs.VideoSizeY));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Bit Rate
            EditorGUILayout.BeginHorizontal();
            rs.BitRateEnabled = GUILayout.Toggle(rs.BitRateEnabled, "Bit Rate", AndroidLogcatStyles.toolbarButton, GUILayout.MaxWidth(width));
            EditorGUI.BeginDisabledGroup(!rs.BitRateEnabled);
            rs.BitRate = Math.Max(1, (uint)EditorGUILayout.IntField(GUIContent.none, (int)rs.BitRate));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Display Id
            EditorGUILayout.BeginHorizontal();
            rs.DisplayIdEnabled = GUILayout.Toggle(rs.DisplayIdEnabled, "Display Id", AndroidLogcatStyles.toolbarButton, GUILayout.MaxWidth(width));
            EditorGUI.BeginDisabledGroup(!rs.DisplayIdEnabled);
            rs.DisplayId = EditorGUILayout.TextField(GUIContent.none, rs.DisplayId);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing debugging buttons:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            if (GUILayout.Button("Delete recording on device", AndroidLogcatStyles.toolbarButton))
            {
                DeleteRecordingOnDevice();
            }
            if (GUILayout.Button("Get screen record pid", AndroidLogcatStyles.toolbarButton))
            {
                var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice, "screenrecord");
                UnityEngine.Debug.Log("screen record pid is " + pid);
            }

            EditorGUILayout.EndHorizontal();
        }

        void DoVideoPlayerGUI()
        {
            if (IsRecording())
            {
                EditorGUILayout.HelpBox("Not available while recording", MessageType.Info);
                return;
            }
            if (m_VideoPlayer == null || m_VideoPlayer.texture == null)
                return;

            EditorGUILayout.BeginHorizontal();
            if (m_VideoPlayer.isPaused)
            {
                if (GUILayout.Button("Play"))
                    m_VideoPlayer.Play();
            }
            else
            {
                if (GUILayout.Button("Pause"))
                    m_VideoPlayer.Pause();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            m_VideoFrame = (long)GUILayout.HorizontalSlider(m_VideoFrame, 0, m_VideoPlayer.frameCount - 1);
            if (EditorGUI.EndChangeCheck())
            {
                m_VideoPlayer.Pause();
                m_VideoPlayer.frame = m_VideoFrame;
            }
            GUILayout.Label(m_VideoPlayer.frame.ToString());
            Repaint();
            float aspect = (float)m_VideoPlayer.width / (float)m_VideoPlayer.height;
            var rc = GUILayoutUtility.GetAspectRect(aspect);
            Rect r1, r2;

            var correctedHeight = Screen.height - rc.y - 20;
            var s = correctedHeight / rc.height;
            r1 = rc;
            r1.width *= s;
            r1.height *= s;

            var correctedWidth = Screen.width - rc.x;
            s = correctedWidth / rc.width;
            r2 = rc;
            r2.width *= s;
            r2.height *= s;

            var videoRect = r1.width < r2.width ? r1 : r2;

            videoRect.x += (Screen.width - videoRect.width) * 0.5f;

            if (m_VideoPlayer.texture != null)
                GUI.DrawTexture(videoRect, m_VideoPlayer.texture);
        }

        void OnGUI()
        {
            if (!DoIsSupportedGUI())
                return;

            CheckRecordingForErrors();

            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            DoProgressGUI(IsRecording());
            DoSelectedDeviceGUI();
            DoRecordingGUI();
            DoSaveGUI();
            EditorGUILayout.EndHorizontal();

            if (Unsupported.IsDeveloperMode())
                DoDebuggingGUI();

            EditorGUI.BeginDisabledGroup(IsRecording());
            DoRecordingSettingsGUI();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);

            var boxRect = GUILayoutUtility.GetLastRect();
            var oldColor = GUI.color;
            GUI.color = Color.grey;
            GUI.Box(new Rect(0, boxRect.y + boxRect.height, Screen.width, Screen.height), GUIContent.none);
            GUI.color = oldColor;

            if (m_RecordingProcessErrors.Length > 0)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox(m_RecordingProcessErrors.ToString(), MessageType.Error);
                return;
            }

            DoVideoPlayerGUI();
        }
    }
}
