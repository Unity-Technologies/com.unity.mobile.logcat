using System;
using System.IO;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// What a screenshot cannot say about itself: the device it came from, and when.
    /// Written beside the image as <c>&lt;name&gt;.json</c> when it is captured, and
    /// moved and deleted with it. A screenshot that has none - captured before this
    /// existed, or dropped into the folder by hand - shows Undefined instead.
    /// </summary>
    [Serializable]
    internal class AndroidLogcatScreenshotInfo
    {
        internal const string kExtension = ".json";

        // Bumped if the fields below stop meaning what they mean now, so a reader can
        // tell an old file from an unreadable one.
        internal const int kVersion = 1;

        public int version = kVersion;
        public string capturedAt;
        public string deviceId;
        public string deviceName;
        public string manufacturer;
        public string model;
        public string osVersion;
        public int apiLevel;
        public string abi;
        public int displayWidth;
        public int displayHeight;

        internal static string PathFor(string imagePath)
        {
            return string.IsNullOrEmpty(imagePath) ? null : Path.ChangeExtension(imagePath, kExtension);
        }

        /// <summary>
        /// Reads the device. Talks to adb for the display size, so this belongs on the
        /// thread the capture itself runs on, not on the GUI's.
        /// </summary>
        internal static AndroidLogcatScreenshotInfo Create(IAndroidLogcatDevice device)
        {
            if (device == null)
                return null;

            var displaySize = Vector2.zero;
            try
            {
                device.QueryDisplaySize(out var physical, out var overriden);
                displaySize = overriden ?? physical;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to query display size: {ex.Message}");
            }

            var manufacturer = device.Manufacturer ?? string.Empty;
            var model = device.Model ?? string.Empty;

            return new AndroidLogcatScreenshotInfo()
            {
                capturedAt = DateTime.Now.ToString("o"),
                deviceId = device.Id,
                deviceName = $"{manufacturer} {model}".Trim(),
                manufacturer = manufacturer,
                model = model,
                osVersion = device.OSVersion?.ToString(),
                apiLevel = device.APILevel,
                abi = device.ABI,
                displayWidth = (int)displaySize.x,
                displayHeight = (int)displaySize.y
            };
        }

        static bool IsDetails(AndroidLogcatScreenshotInfo info)
        {
            return info != null && info.version > 0 && !string.IsNullOrEmpty(info.capturedAt);
        }

        static bool MayReplace(string path, string what)
        {
            if (!File.Exists(path))
                return true;

            try
            {
                if (IsDetails(JsonUtility.FromJson<AndroidLogcatScreenshotInfo>(File.ReadAllText(path))))
                    return true;
            }
            catch (Exception)
            {
                // Not readable as ours, so certainly not ours.
            }

            UnityEngine.Debug.LogWarning($"Not {what} '{path}': it was not written by Android Logcat.");
            return false;
        }

        internal void Save(string imagePath)
        {
            var path = PathFor(imagePath);
            if (!MayReplace(path, "overwriting"))
                return;

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(this, true));
            }
            catch (Exception ex)
            {
                // A screenshot without its details is still a screenshot, so this is
                // logged rather than failing the capture.
                UnityEngine.Debug.LogWarning($"Failed to write '{path}'.\n{ex.Message}");
            }
        }

        /// <summary>Returns null when there is no file, or it cannot be read.</summary>
        internal static AndroidLogcatScreenshotInfo Load(string imagePath)
        {
            var path = PathFor(imagePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                var info = JsonUtility.FromJson<AndroidLogcatScreenshotInfo>(File.ReadAllText(path));
                // A file of someone else's that shares the name is not details, and
                // its defaults would be shown as a device of empty strings and zeroes.
                return IsDetails(info) ? info : null;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to read '{path}': {ex.Message}");
                return null;
            }
        }

        internal static void Move(string fromImagePath, string toImagePath)
        {
            if (!Both(fromImagePath, toImagePath, out var from, out var to))
                return;
            if (!MayReplace(to, "overwriting"))
                return;

            try
            {
                File.Delete(to);
                File.Move(from, to);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to move '{from}' to '{to}'.\n{ex.Message}");
            }
        }

        /// <summary>Takes the details along to a copy of the image saved elsewhere.</summary>
        internal static void CopyBeside(string fromImagePath, string toImagePath)
        {
            if (!Both(fromImagePath, toImagePath, out var from, out var to))
                return;
            if (!MayReplace(to, "overwriting"))
                return;

            try
            {
                File.Copy(from, to, true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to copy '{from}' to '{to}'.\n{ex.Message}");
            }
        }

        static bool Both(string fromImagePath, string toImagePath, out string from, out string to)
        {
            from = PathFor(fromImagePath);
            to = PathFor(toImagePath);
            return from != null && to != null && from != to && File.Exists(from);
        }

        internal static void Delete(string imagePath)
        {
            var path = PathFor(imagePath);
            if (path == null || !File.Exists(path))
                return;
            if (!MayReplace(path, "deleting"))
                return;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to delete '{path}'.\n{ex.Message}");
            }
        }
    }
}
