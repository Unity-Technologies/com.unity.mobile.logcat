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
        private AndroidLogcatScreenRecorder m_Recorder;
        private VideoPlayer m_VideoPlayer;
        private long m_VideoFrame;

        private bool m_DbgCleanupRecordingOnDevice = true;

        private bool CleanupRecordingOnDevice
        {
            get
            {
                if (!Unsupported.IsDeveloperMode())
                    return true;
                return m_DbgCleanupRecordingOnDevice;
            }
            set
            {
                m_DbgCleanupRecordingOnDevice = value;
            }
        }

        public static void ShowWindow()
        {
            var win = EditorWindow.GetWindow<AndroidLogcatScreenRecorderWindow>("Device Video Capture");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_Recorder = m_Runtime.ScreenRecorder;
            m_VideoPlayer = AcquireVideoPlayer();
        }

        protected override void OnDisable()
        {
            if (m_Runtime == null)
                return;
            // TODO
            UnityEngine.Debug.Log("Disable");

            m_VideoPlayer.Stop();
            m_Recorder = null;
            base.OnDisable();
        }

        private VideoPlayer AcquireVideoPlayer()
        {
            var name = "LogcatVideoPlayer";
            var go = GameObject.Find(name);
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

        void DoRecordingGUI()
        {
            if (m_Recorder.IsRecording())
            {
                if (GUILayout.Button(kStopRecording, AndroidLogcatStyles.toolbarButton))
                {
                    var device = m_Recorder.RecordingOnDevice;
                    m_Recorder.StopRecording();
                    m_Recorder.CopyRecordingFromDevice(device);
                    if (CleanupRecordingOnDevice)
                        m_Recorder.DeleteRecordingOnDevice(device);

                    m_VideoPlayer.url = m_Recorder.RecordingSavePath;
                    m_VideoPlayer.Play();
                }
            }
            else
            {
                if (GUILayout.Button(kStartRecording, AndroidLogcatStyles.toolbarButton))
                {
                    m_Recorder.DeleteTempVideo();
                    m_Recorder.StartRecording(SelectedDevice);
                }
            }
        }

        void DoSaveGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(m_Recorder.RecordingSavePath));
            if (GUILayout.Button("Save...", AndroidLogcatStyles.toolbarButton, GUILayout.Width(kSaveButtonWidth)))
            {
                var path = EditorUtility.SaveFilePanel("Save Screen Capture", "", Path.GetFileName(m_Recorder.RecordingSavePath), Path.GetExtension(m_Recorder.RecordingSavePath));
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.Copy(m_Recorder.RecordingSavePath, path, true);
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


            // TODO tooltips
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
                m_Recorder.DeleteRecordingOnDevice(SelectedDevice);
            }
            if (GUILayout.Button("Get screen record pid", AndroidLogcatStyles.toolbarButton))
            {
                var pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, m_Runtime.DeviceQuery.SelectedDevice, "screenrecord");
                UnityEngine.Debug.Log("screen record pid is " + pid);
            }

            CleanupRecordingOnDevice = GUILayout.Toggle(CleanupRecordingOnDevice, "Clean up Recording");
            if (GUILayout.Button("Copy Recording from device", AndroidLogcatStyles.toolbarButton))
                m_Recorder.CopyRecordingFromDevice(SelectedDevice);


            EditorGUILayout.EndHorizontal();
        }

        void DoVideoPlayerGUI()
        {
            if (m_Recorder.IsRecording())
            {
                EditorGUILayout.HelpBox($"Recording{new String('.', (int)(Time.realtimeSinceStartup * 3) % 4 + 1)}", MessageType.Info);
                return;
            }
            if (m_VideoPlayer == null || m_VideoPlayer.texture == null)
            {
                EditorGUILayout.HelpBox("No video to show.", MessageType.Info);
                return;
            }

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

            if (m_Recorder.IsRecording() && Event.current.type == EventType.Repaint)
                Repaint();


            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            DoProgressGUI(false);
            DoSelectedDeviceGUI();

            EditorGUI.BeginDisabledGroup(SelectedDevice == null);
            {
                DoRecordingGUI();
                DoSaveGUI();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (Unsupported.IsDeveloperMode())
                DoDebuggingGUI();

            EditorGUI.BeginDisabledGroup(m_Recorder.IsRecording());
            DoRecordingSettingsGUI();

            EditorGUILayout.EndVertical();

            EditorGUI.EndDisabledGroup();
            GUILayout.Space(5);

            var boxRect = GUILayoutUtility.GetLastRect();
            var oldColor = GUI.color;
            GUI.color = Color.grey;
            GUI.Box(new Rect(0, boxRect.y + boxRect.height, Screen.width, Screen.height), GUIContent.none);
            GUI.color = oldColor;

            if (m_Recorder.Errors.Length > 0)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox(m_Recorder.Errors, MessageType.Error);
                return;
            }

            DoVideoPlayerGUI();
        }
    }
}
