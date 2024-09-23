using System;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCaptureScreenshot
    {
        internal class AndroidLogcatCaptureScreenCaptureInput : IAndroidLogcatTaskInput
        {
            internal AndroidBridge.ADB adb;
            internal string imagePath;
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
        private Texture2D m_ImageTexture = null;
        private int m_CaptureCount;
        private string m_Error;
        private Rect m_ScreenshotDrawingRect;

        public bool IsCapturing => m_CaptureCount > 0;
        public Texture2D ImageTexture => m_ImageTexture;
        public string Error => m_Error;
        public Rect ScreenshotDrawingRect => m_ScreenshotDrawingRect;
        public string GetImagePath(IAndroidLogcatDevice device)
        {
            if (device == null)
                return string.Empty;
            return AndroidLogcatUtilities.GetTemporaryPath(device, "screenshot", GetImageExtension());
        }

        public string GetImageExtension()
        {
            return ".png";
        }

        internal AndroidLogcatCaptureScreenshot(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
        }

        public void QueueScreenCapture(IAndroidLogcatDevice device, Action onCompleted)
        {
            if (device == null)
                return;

            m_Runtime.Dispatcher.Schedule(
                new AndroidLogcatCaptureScreenCaptureInput()
                {
                    adb = m_Runtime.Tools.ADB,
                    imagePath = GetImagePath(device),
                    deviceId = device.Id,
                    onCompleted = onCompleted
                },
                ExecuteScreenCapture,
                IntegrateCaptureScreenShot,
                false);
            m_CaptureCount++;
        }

        private static IAndroidLogcatTaskResult ExecuteScreenCapture(IAndroidLogcatTaskInput input)
        {
            var i = (AndroidLogcatCaptureScreenCaptureInput)input;
            var result = AndroidLogcatUtilities.CaptureScreen(i.adb, i.deviceId, i.imagePath, out var error);

            return new AndroidLogcatCaptureScreenCaptureResult()
            {
                imagePath = result ? i.imagePath : null,
                error = error,
                onCompleted = i.onCompleted
            };
        }

        private void IntegrateCaptureScreenShot(IAndroidLogcatTaskResult result)
        {
            if (m_CaptureCount > 0)
                m_CaptureCount--;
            var captureResult = (AndroidLogcatCaptureScreenCaptureResult)result;
            m_Error = captureResult.error;
            LoadImage(captureResult.imagePath);

            captureResult.onCompleted();
        }

        public void LoadImage(string imagePath)
        {
            m_ImageTexture = null;

            if (string.IsNullOrEmpty(imagePath))
                return;
            if (!File.Exists(imagePath))
                return;

            var imageData = File.ReadAllBytes(imagePath);

            m_ImageTexture = new Texture2D(2, 2);
            if (!m_ImageTexture.LoadImage(imageData))
                return;
        }

        public bool DoGUI(Rect rc)
        {
            if (!string.IsNullOrEmpty(m_Error))
            {
                EditorGUI.HelpBox(rc, m_Error, MessageType.Error);
            }
            else if (m_ImageTexture != null)
            {
                GUI.DrawTexture(rc, m_ImageTexture, ScaleMode.ScaleToFit);
                m_ScreenshotDrawingRect = GUILayoutUtility.GetLastRect();

                var imageAspect = (float)m_ImageTexture.width / (float)m_ImageTexture.height;
                var windowAspect = m_ScreenshotDrawingRect.width / m_ScreenshotDrawingRect.height;
                if (imageAspect < windowAspect)
                {
                    var width = m_ScreenshotDrawingRect.height * imageAspect;
                    m_ScreenshotDrawingRect = new Rect((m_ScreenshotDrawingRect.width - width) * 0.5f, m_ScreenshotDrawingRect.y, width, m_ScreenshotDrawingRect.height);
                }
                else
                {
                    var height = m_ScreenshotDrawingRect.width / imageAspect;
                    m_ScreenshotDrawingRect = new Rect(m_ScreenshotDrawingRect.x, (m_ScreenshotDrawingRect.height - height) * 0.5f, m_ScreenshotDrawingRect.width, height);
                }
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
