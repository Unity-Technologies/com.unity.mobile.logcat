using System;
using System.IO;
using UnityEngine;
using UnityEditor;
#if PLATFORM_ANDROID
using UnityEditor.Android;
#endif

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCaptureWindow : EditorWindow
    {
        [SerializeField] private string m_ImagePath;
#if PLATFORM_ANDROID
        private IAndroidLogcatRuntime m_Runtime;
        private string m_DeviceId;
        private Texture2D m_ImageTexture = null;
        private int m_CaptureCount;
        private const int kButtonAreaHeight = 30;
        private const int kBottomAreaHeight = 8;

        internal class AndroidLogcatCaptureScreenCaptureInput : IAndroidLogcatTaskInput
        {
            internal ADB adb;
            internal string deviceId;
        }

        internal class AndroidLogcatCaptureScreenCaptureResult : IAndroidLogcatTaskResult
        {
            internal string imagePath;
        }

        public static void Show(string deviceId)
        {
            AndroidLogcatScreenCaptureWindow win = EditorWindow.GetWindow<AndroidLogcatScreenCaptureWindow>("Device Screen Capture");
            win.m_DeviceId = deviceId;
            win.QueueScreenCapture();
        }

        private void OnEnable()
        {
            m_Runtime = AndroidLogcatManager.instance.Runtime;
        }

        private void QueueScreenCapture()
        {
            m_Runtime.Dispatcher.Schedule(
                new AndroidLogcatCaptureScreenCaptureInput() { adb = m_Runtime.Tools.ADB, deviceId = m_DeviceId},
                ExecuteScreenCapture,
                IntegrateCaptureScreenShot,
                false);
            m_CaptureCount++;
        }

        private static IAndroidLogcatTaskResult ExecuteScreenCapture(IAndroidLogcatTaskInput input)
        {
            var i = (AndroidLogcatCaptureScreenCaptureInput)input;
            return new AndroidLogcatCaptureScreenCaptureResult()
            {
                imagePath = AndroidLogcatUtilities.CaptureScreen(i.adb, i.deviceId)
            };
        }

        private void IntegrateCaptureScreenShot(IAndroidLogcatTaskResult result)
        {
            if (m_CaptureCount > 0)
                m_CaptureCount--;
            m_ImagePath = ((AndroidLogcatCaptureScreenCaptureResult)result).imagePath;
            if (!string.IsNullOrEmpty(m_ImagePath))
                LoadImage();
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            GUIContent statusIcon = GUIContent.none;
            if (m_CaptureCount > 0)
            {
                int frame = (int)Mathf.Repeat(Time.realtimeSinceStartup * 10, 11.99f);
                statusIcon = AndroidLogcatStyles.Status.GetContent(frame);
                Repaint();
            }
            GUILayout.Button(statusIcon, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));

            EditorGUI.BeginDisabledGroup(m_CaptureCount > 0);
            if (GUILayout.Button("Capture", AndroidLogcatStyles.toolbarButton))
                QueueScreenCapture();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Save...", AndroidLogcatStyles.toolbarButton))
            {
                var path = EditorUtility.SaveFilePanel("Save Screen Capture", "", Path.GetFileName(m_ImagePath), "png");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.Copy(m_ImagePath, path, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogErrorFormat("Failed to save to '{0}' as '{1}'.", path, ex.Message);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (m_ImageTexture != null)
            {
                Rect rect = new Rect(0, kButtonAreaHeight, position.width, position.height - kButtonAreaHeight - kBottomAreaHeight);
                GUI.DrawTexture(rect, m_ImageTexture, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndVertical();
        }

        void LoadImage()
        {
            if (!File.Exists(m_ImagePath))
                return;

            byte[] imageData;
            imageData = File.ReadAllBytes(m_ImagePath);

            m_ImageTexture = new Texture2D(2, 2); // The size will be replaced by LoadImage().
            if (!m_ImageTexture.LoadImage(imageData))
                return;

            maxSize = new Vector2(Math.Max(m_ImageTexture.width, position.width), m_ImageTexture.height + kButtonAreaHeight);
        }

#else
        internal void OnGUI()
        {
            AndroidLogcatUtilities.ShowActivePlatformNotAndroidMessage();
        }

#endif
    }
}
