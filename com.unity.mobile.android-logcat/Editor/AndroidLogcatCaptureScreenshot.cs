using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCapture
    {
        internal class AndroidLogcatCaptureScreenCaptureInput : IAndroidLogcatTaskInput
        {
            internal AndroidBridge.ADB adb;
            internal string deviceId;
            internal Action onCompleted;
        }

        internal class AndroidLogcatCaptureScreenCaptureResult : IAndroidLogcatTaskResult
        {
            internal string imagePath;
            internal string error;
            internal Action onCompleted;
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private string m_ImagePath;
        private Texture2D m_ImageTexture = null;
        private int m_CaptureCount;
        private string m_Error;

        public bool Capturing => m_CaptureCount > 0;
        public Texture2D ImageTexture => m_ImageTexture;
        public string Error => m_Error;

        internal AndroidLogcatScreenCapture(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
        }

        public void QueueScreenCapture(IAndroidLogcatDevice device, Action onCompleted)
        {
            if (device == null)
                return;

            m_Runtime.Dispatcher.Schedule(
                new AndroidLogcatCaptureScreenCaptureInput() { adb = m_Runtime.Tools.ADB, deviceId = device.Id, onCompleted = onCompleted },
                ExecuteScreenCapture,
                IntegrateCaptureScreenShot,
                false);
            m_CaptureCount++;
        }

        private static IAndroidLogcatTaskResult ExecuteScreenCapture(IAndroidLogcatTaskInput input)
        {
            var i = (AndroidLogcatCaptureScreenCaptureInput)input;
            var path = AndroidLogcatUtilities.CaptureScreen(i.adb, i.deviceId, out var error);

            return new AndroidLogcatCaptureScreenCaptureResult()
            {
                imagePath = path,
                error = error,
                onCompleted = i.onCompleted
            };
        }

        private void IntegrateCaptureScreenShot(IAndroidLogcatTaskResult result)
        {
            if (m_CaptureCount > 0)
                m_CaptureCount--;
            var captureResult = (AndroidLogcatCaptureScreenCaptureResult)result;
            m_ImagePath = captureResult.imagePath;
            m_Error = captureResult.error;
            if (!string.IsNullOrEmpty(m_ImagePath))
                LoadImage();

            captureResult.onCompleted();
        }

        void LoadImage()
        {
            if (!File.Exists(m_ImagePath))
                return;

            var imageData = File.ReadAllBytes(m_ImagePath);

            m_ImageTexture = new Texture2D(2, 2);
            if (!m_ImageTexture.LoadImage(imageData))
                return;
        }

        public void DoGUI(Rect rc)
        {
            if (!string.IsNullOrEmpty(m_Error))
            {
                EditorGUILayout.HelpBox(m_Error, MessageType.Error);
            }
            else if (m_ImageTexture != null)
            {
                GUI.DrawTexture(rc, m_ImageTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("No screenshot to show, click Capture button.", MessageType.Info);
            }
        }

        public void DoSaveAsGUI()
        {
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
                        UnityEngine.Debug.LogErrorFormat("Failed to save to '{0}' as '{1}'.", path, ex.Message);
                    }
                }
            }
        }

    }
}
