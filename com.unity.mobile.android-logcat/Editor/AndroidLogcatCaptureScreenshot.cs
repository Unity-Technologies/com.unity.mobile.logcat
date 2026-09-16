using System;
using System.Collections.Generic;
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
            internal string deviceId;
            internal string error;
            internal Action onCompleted;
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private Texture2D m_ImageTexture = null;
        private int m_CaptureCount;
        private string m_Error;
        private Rect m_ScreenshotDrawingRect;

        /// <summary>One saved screenshot on disk.</summary>
        internal readonly struct Screenshot
        {
            internal string Path { get; }
            /// <summary>File name without extension, which is what the list view shows.</summary>
            internal string Name { get; }
            /// <summary>
            /// The device part of the file name. This is the sanitized device id, so
            /// comparing it to a device means sanitizing that id too.
            /// </summary>
            internal string DevicePrefix { get; }
            internal int Number { get; }

            internal Screenshot(string path, string devicePrefix, int number)
            {
                Path = path;
                Name = System.IO.Path.GetFileNameWithoutExtension(path);
                DevicePrefix = devicePrefix;
                Number = number;
            }
        }

        // Every saved screenshot, of every device. Cached because the window asks for
        // this from OnGUI, and scanning the directory every repaint would be disk I/O
        // per frame. Rescanned when a capture lands.
        private List<Screenshot> m_Screenshots;

        // What LoadImage last put on screen, which is what Open and Save As act on.
        private string m_SelectedImagePath;

        public bool IsCapturing => m_CaptureCount > 0;
        public Texture2D ImageTexture => m_ImageTexture;
        public string Error => m_Error;
        public Rect ScreenshotDrawingRect => m_ScreenshotDrawingRect;

        /// <summary>The screenshot currently displayed, or empty if there is none.</summary>
        public string SelectedImagePath => m_SelectedImagePath;

        /// <summary>
        /// Every saved screenshot, of every device, grouped by device and numbered
        /// ascending within each.
        /// </summary>
        public IReadOnlyList<Screenshot> GetScreenshots()
        {
            if (m_Screenshots == null)
                m_Screenshots = ScanScreenshots();
            return m_Screenshots;
        }

        /// <summary>
        /// The most recent screenshot captured for this device, or empty when there is
        /// none. Screenshots are numbered rather than overwritten, so "the" path is
        /// whichever one was taken last.
        /// </summary>
        public string GetLatestImagePath(IAndroidLogcatDevice device)
        {
            if (device == null)
                return string.Empty;

            var prefix = AndroidLogcatUtilities.SanitizeFileName(device.Id);
            var screenshots = GetScreenshots();
            // Ordered by number within a device, so the last match is the newest.
            for (var i = screenshots.Count - 1; i >= 0; i--)
            {
                if (screenshots[i].DevicePrefix == prefix)
                    return screenshots[i].Path;
            }
            return string.Empty;
        }

        /// <summary>
        /// Reserves the next free path, <c>&lt;device_id&gt;_&lt;number&gt;.png</c> under
        /// <see cref="AndroidLogcatUtilities.GetScreenshotsDirectory"/>, and makes sure
        /// the directory exists - adb pull will not create it.
        /// </summary>
        private string AllocateImagePath(IAndroidLogcatDevice device)
        {
            var directory = AndroidLogcatUtilities.GetScreenshotsDirectory();
            Directory.CreateDirectory(directory);

            var prefix = AndroidLogcatUtilities.SanitizeFileName(device.Id);
            var screenshots = GetScreenshots();

            // Numbering is per device, so only this device's entries count.
            var number = 1;
            foreach (var screenshot in screenshots)
            {
                if (screenshot.DevicePrefix == prefix && screenshot.Number >= number)
                    number = screenshot.Number + 1;
            }

            var path = Path.Combine(directory, $"{prefix}_{number}{GetImageExtension()}").Replace("\\", "/");

            // The reservation goes straight into the list, which is what stops a second
            // capture queued before this file exists from picking the same number - the
            // list is counted from, not the directory. The completion handler rescans,
            // which both picks up the real file and drops this entry if the capture
            // failed. The Layout Viewer can queue captures without waiting for the
            // previous one, so this is reachable.
            m_Screenshots.Add(new Screenshot(path, prefix, number));
            m_Screenshots.Sort(CompareScreenshots);
            return path;
        }

        private List<Screenshot> ScanScreenshots()
        {
            var screenshots = new List<Screenshot>();
            var directory = AndroidLogcatUtilities.GetScreenshotsDirectory();
            if (!Directory.Exists(directory))
                return screenshots;

            foreach (var file in Directory.GetFiles(directory, $"*{GetImageExtension()}"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                // Split at the last underscore: a device id can contain one itself once
                // sanitized, e.g. an ip:port becomes 192.168.1.5_5555, so only the part
                // after the final underscore is the number.
                var separator = name.LastIndexOf('_');
                if (separator <= 0)
                    continue;
                if (!int.TryParse(name.Substring(separator + 1), out var number))
                    continue;

                screenshots.Add(new Screenshot(
                    file.Replace("\\", "/"),
                    name.Substring(0, separator),
                    number));
            }

            screenshots.Sort(CompareScreenshots);
            return screenshots;
        }

        /// <summary>
        /// Groups by device, then orders by number. GetFiles order is filesystem
        /// dependent, and sorting the names as strings would put #10 before #2.
        /// </summary>
        private static int CompareScreenshots(Screenshot a, Screenshot b)
        {
            var byDevice = string.Compare(a.DevicePrefix, b.DevicePrefix, StringComparison.Ordinal);
            return byDevice != 0 ? byDevice : a.Number.CompareTo(b.Number);
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
                    // Allocated here on the main thread, before the task is scheduled.
                    imagePath = AllocateImagePath(device),
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
                deviceId = i.deviceId,
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

            // Drop the cache so the new file appears in the list, and so a reservation
            // made by AllocateImagePath disappears again if the capture failed. One
            // rescan per capture, rather than per repaint, which is what the cache is
            // there for.
            m_Screenshots = null;

            LoadImage(captureResult.imagePath);

            captureResult.onCompleted();
        }

        public void LoadImage(string imagePath)
        {
            m_ImageTexture = null;
            m_SelectedImagePath = string.Empty;

            if (string.IsNullOrEmpty(imagePath))
                return;
            if (!File.Exists(imagePath))
                return;

            // Normalized so it compares equal to the paths in the screenshot list,
            // which the list view uses to mark the selected row.
            m_SelectedImagePath = imagePath.Replace("\\", "/");

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
