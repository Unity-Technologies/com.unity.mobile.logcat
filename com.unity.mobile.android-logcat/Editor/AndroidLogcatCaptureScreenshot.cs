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
            internal IAndroidLogcatDevice device;
            internal Action onCompleted;
        }

        internal class AndroidLogcatCaptureScreenCaptureResult : IAndroidLogcatTaskResult
        {
            internal string imagePath;
            // The path AllocateImagePath handed out, which is imagePath on success and
            // still needed on failure - that is the reservation to release.
            internal string reservedPath;
            internal string deviceId;
            internal string error;
            internal AndroidLogcatScreenshotInfo info;
            internal Action onCompleted;
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        // Where captures are kept, and whether previous ones are kept with them. The
        // Screen Capture window numbers its screenshots and keeps them all; the Layout
        // Viewer has its own directory holding one.
        private readonly string m_Directory;
        private readonly bool m_KeepHistory;
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

        // Paths handed out for captures that have not produced a file yet. Held apart
        // from the cache above, because dropping that cache must not lose them: a
        // capture still in flight has to keep its number reserved or the next capture
        // takes the same one and overwrites it. A rescan puts them back.
        private readonly HashSet<string> m_ReservedPaths = new HashSet<string>();

        // What LoadImage last put on screen, which is what Open and Save As act on.
        private string m_SelectedImagePath;

        public bool IsCapturing => m_CaptureCount > 0;
        public Texture2D ImageTexture => m_ImageTexture;
        public string Error => m_Error;
        public Rect ScreenshotDrawingRect => m_ScreenshotDrawingRect;

        /// <summary>The screenshot currently displayed, or empty if there is none.</summary>
        public string SelectedImagePath => m_SelectedImagePath;

        /// <summary>
        /// Drops the cached listing so the next <see cref="GetScreenshots"/> reads the
        /// directory again. For changes this class did not make - a file added, removed
        /// or replaced from outside the Editor - which nothing else can notice.
        /// </summary>
        public void InvalidateScreenshots()
        {
            m_Screenshots = null;
        }

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
        /// the capture directory, and makes sure it exists - adb pull will not create it.
        /// </summary>
        private string AllocateImagePath(IAndroidLogcatDevice device)
        {
            var directory = m_Directory;
            Directory.CreateDirectory(directory);

            var prefix = AndroidLogcatUtilities.SanitizeFileName(device.Id);
            var screenshots = GetScreenshots();

            if (!m_KeepHistory)
                return AllocateSingleImagePath(directory, prefix);

            // Numbering is per device, so only this device's entries count.
            var number = 1;
            foreach (var screenshot in screenshots)
            {
                if (screenshot.DevicePrefix == prefix && screenshot.Number >= number)
                    number = screenshot.Number + 1;
            }

            var path = Path.Combine(directory, $"{prefix}_{number}{GetImageExtension()}").Replace("\\", "/");

            // The reservation is what stops a second capture queued before this file
            // exists from picking the same number - the list is counted from, not the
            // directory. It is both recorded and added to the live list, so it survives
            // a rescan and is visible to the next allocation either way. The completion
            // handler releases it.
            m_ReservedPaths.Add(path);
            m_Screenshots.Add(new Screenshot(path, prefix, number));
            m_Screenshots.Sort(CompareScreenshots);
            return path;
        }

        /// <summary>
        /// The single slot of a capture that keeps no history: always the same file,
        /// with everything captured before it - including a capture of another device -
        /// deleted, so the directory holds one screenshot and no more.
        /// </summary>
        private string AllocateSingleImagePath(string directory, string prefix)
        {
            var path = Path.Combine(directory, $"{prefix}_1{GetImageExtension()}").Replace("\\", "/");

            foreach (var screenshot in GetScreenshots())
            {
                // Not a capture in flight: that file is about to be written.
                if (screenshot.Path == path || m_ReservedPaths.Contains(screenshot.Path))
                    continue;
                DeleteQuietly(screenshot.Path);
                AndroidLogcatScreenshotInfo.Delete(screenshot.Path);
            }

            m_ReservedPaths.Add(path);
            // Rather than editing the cached list: the deletions above have already
            // made it wrong, and the rescan puts the reservation back.
            InvalidateScreenshots();
            return path;
        }

        private List<Screenshot> ScanScreenshots()
        {
            var screenshots = new List<Screenshot>();
            var directory = m_Directory;
            if (!Directory.Exists(directory))
                return screenshots;

            foreach (var file in Directory.GetFiles(directory, $"*{GetImageExtension()}"))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                // Split at the last underscore: a device id can contain one itself once
                // sanitized, e.g. an ip:port becomes 192.168.1.5_5555, so only the part
                // after the final underscore is the number.
                //
                // A name that does not match is still listed, with no device and no
                // number. Renaming is allowed, and a file dropped in here by hand should
                // show up too - being unable to see a file that is plainly in the folder
                // would be worse than not knowing which device it came from.
                var devicePrefix = string.Empty;
                var number = 0;
                var separator = name.LastIndexOf('_');
                if (separator > 0 && int.TryParse(name.Substring(separator + 1), out number))
                    devicePrefix = name.Substring(0, separator);
                else
                    number = 0;

                screenshots.Add(new Screenshot(file.Replace("\\", "/"), devicePrefix, number));
            }

            // Captures that are still in flight have no file yet, so a scan would not
            // see them - and the number they reserved would be handed out twice.
            foreach (var reserved in m_ReservedPaths)
            {
                if (File.Exists(reserved))
                    continue;

                var name = Path.GetFileNameWithoutExtension(reserved);
                var separator = name.LastIndexOf('_');
                if (separator > 0 && int.TryParse(name.Substring(separator + 1), out var reservedNumber))
                    screenshots.Add(new Screenshot(reserved, name.Substring(0, separator), reservedNumber));
            }

            screenshots.Sort(CompareScreenshots);
            return screenshots;
        }

        /// <summary>
        /// Groups by device, then orders by number. GetFiles order is filesystem
        /// dependent, and sorting the names as strings would put #10 before #2. Renamed
        /// files have no device or number, so they sort last, by name.
        /// </summary>
        private static int CompareScreenshots(Screenshot a, Screenshot b)
        {
            var aNamed = string.IsNullOrEmpty(a.DevicePrefix);
            var bNamed = string.IsNullOrEmpty(b.DevicePrefix);
            if (aNamed != bNamed)
                return aNamed ? 1 : -1;
            if (aNamed)
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);

            var byDevice = string.Compare(a.DevicePrefix, b.DevicePrefix, StringComparison.Ordinal);
            return byDevice != 0 ? byDevice : a.Number.CompareTo(b.Number);
        }

        /// <summary>
        /// Renames a saved screenshot, keeping it in the same directory and keeping its
        /// extension. A name that no longer matches
        /// <c>&lt;device_id&gt;_&lt;number&gt;</c> is fine: it stays in the list, just
        /// without a device or a number, and is never picked as "the latest" for a device.
        /// </summary>
        /// <returns>False if the name is unusable or the move failed, which is logged.</returns>
        public bool RenameScreenshot(string path, string newName)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            newName = newName == null ? string.Empty : newName.Trim();
            if (newName.Length == 0)
                return false;

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                UnityEngine.Debug.LogError($"'{newName}' is not a usable file name. " +
                    "File names cannot contain \\ / : * ? \" < > | or control characters. " +
                    "Choose a different name.");
                return false;
            }

            var directory = Path.GetDirectoryName(path);
            var target = Path.Combine(directory, newName + GetImageExtension()).Replace("\\", "/");
            if (target == path)
                return true;

            if (File.Exists(target))
            {
                UnityEngine.Debug.LogError(
                    $"'{newName}{GetImageExtension()}' already exists. Choose a different name.");
                return false;
            }

            // Checked before the image moves, so the two cannot end up apart.
            if (!AndroidLogcatScreenshotInfo.CanWriteBeside(target))
            {
                UnityEngine.Debug.LogError(
                    $"'{Path.GetFileName(AndroidLogcatScreenshotInfo.PathFor(target))}' already exists " +
                    "and was not written by Android Logcat. Choose a different name.");
                return false;
            }

            try
            {
                File.Move(path, target);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to rename '{path}' to '{target}'.\n{ex.Message}");
                return false;
            }

            AndroidLogcatScreenshotInfo.Move(path, target);

            // Rescan, so the list picks up the new name and reorders.
            InvalidateScreenshots();

            // Keep showing the same image, now under its new path.
            if (m_SelectedImagePath == path)
                m_SelectedImagePath = target;

            return true;
        }

        /// <summary>
        /// Removes a saved screenshot from disk. If it was the one on screen, the image
        /// is cleared too - the caller decides what to show instead.
        /// </summary>
        /// <returns>False if the file could not be removed, which is already logged.</returns>
        public bool DeleteScreenshot(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to delete '{path}'.\n{ex.Message}");
                return false;
            }

            AndroidLogcatScreenshotInfo.Delete(path);

            // Rescan, so the list loses the row.
            InvalidateScreenshots();

            if (m_SelectedImagePath == path)
                LoadImage(string.Empty);

            return true;
        }

        public string GetImageExtension()
        {
            return ".png";
        }

        internal AndroidLogcatCaptureScreenshot(AndroidLogcatRuntimeBase runtime, string directory, bool keepHistory)
        {
            m_Runtime = runtime;
            m_Directory = directory;
            m_KeepHistory = keepHistory;
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
                    device = device,
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
                reservedPath = i.imagePath,
                deviceId = i.deviceId,
                error = error,
                // Read here because it asks the device; written on the main thread,
                // where JsonUtility is safe to call.
                info = result ? AndroidLogcatScreenshotInfo.Create(i.device) : null,
                onCompleted = i.onCompleted
            };
        }

        static void DeleteQuietly(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to delete '{path}': {ex.Message}");
            }
        }

        private void IntegrateCaptureScreenShot(IAndroidLogcatTaskResult result)
        {
            if (m_CaptureCount > 0)
                m_CaptureCount--;
            var captureResult = (AndroidLogcatCaptureScreenCaptureResult)result;
            m_Error = captureResult.error;

            // This capture's reservation is done with: the file either exists now or
            // never will. Only this one is released - reservations for captures still
            // in flight have to stand, which is why they do not live in the cache.
            m_ReservedPaths.Remove(captureResult.reservedPath);

            captureResult.info?.Save(captureResult.imagePath);

            // A pull that failed part way still leaves what it had written, and the
            // rescan below would list that as a screenshot.
            if (string.IsNullOrEmpty(captureResult.imagePath))
                DeleteQuietly(captureResult.reservedPath);

            // Drop the cache so the new file appears in the list, and so a failed
            // capture's entry disappears again. One rescan per capture, rather than per
            // repaint, which is what the cache is there for.
            InvalidateScreenshots();

            LoadImage(captureResult.imagePath);

            captureResult.onCompleted();
        }

        /// <summary>
        /// Records which screenshot is selected without touching
        /// <see cref="ImageTexture"/>, for a window that shows the image itself.
        /// <para>
        /// The texture here is the last capture, which is what a window drawing an
        /// overlay over it has queried against. Loading a historical screenshot into it
        /// from the capture list would put that overlay on an unrelated image, so the
        /// list keeps its own texture and only the path is shared.
        /// </para>
        /// </summary>
        public void SelectImage(string imagePath)
        {
            m_SelectedImagePath = string.IsNullOrEmpty(imagePath)
                ? string.Empty
                : imagePath.Replace("\\", "/");

            // As in LoadImage: an image to show supersedes the last capture's error.
            if (!string.IsNullOrEmpty(m_SelectedImagePath))
                m_Error = string.Empty;
        }

        public void LoadImage(string imagePath)
        {
            m_ImageTexture = null;
            m_SelectedImagePath = string.Empty;

            if (string.IsNullOrEmpty(imagePath))
                return;
            if (!File.Exists(imagePath))
                return;

            // An image to show supersedes the last capture's error, which DoGUI draws
            // in preference to the texture and nothing else would ever clear - leaving
            // every saved screenshot hidden behind it until a capture succeeded. Done
            // after the returns above, so a failed capture keeps the error it just set.
            m_Error = string.Empty;

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
