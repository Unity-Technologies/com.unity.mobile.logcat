using System;
using System.IO;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine;

namespace Unity.Android.Logcat
{
    public class Workspace
    {
        public static bool IsRunningOnYamato()
        {
            return Environment.GetEnvironmentVariable("YAMATO_PROJECT_ID") != null;
        }

        public static bool IsRunningOnKatana()
        {
            return Environment.GetEnvironmentVariable("UNITY_THISISABUILDMACHINE") == "1";
        }

        public static bool IsRunningOnBuildServer()
        {
            return IsRunningOnYamato() || IsRunningOnKatana();
        }

        public static string GetAndroidDeviceInfo()
        {
            var result = Environment.GetEnvironmentVariable("ANDROID_DEVICE_CONNECTION");
            if (result == null)
                return string.Empty;
            return result;
        }

        /// <summary>
        /// Whether the job this runs in has a device attached. Stated by the job rather
        /// than inferred from ANDROID_DEVICE_CONNECTION, which only holds something
        /// when the device is reached over the network - a device on a usb cable looked
        /// like no device at all, and every test that needs one was quietly ignored.
        /// </summary>
        public static bool IsAndroidDeviceAvailable()
        {
            var value = Environment.GetEnvironmentVariable("ANDROID_DEVICE_AVAILABLE");
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetAritfactsPath()
        {
            if (!Workspace.IsRunningOnBuildServer())
                return Path.Combine(Application.dataPath, "../LocalTestResults");

            var result = Environment.GetEnvironmentVariable("ARTIFACTS_PATH");
            if (string.IsNullOrEmpty(result))
                throw new Exception("Couldn't get ARTIFACTS_PATH env variable, maybe env variable is not set?");
            return Path.Combine(Application.dataPath, "../../../", result);
        }
    }
}
