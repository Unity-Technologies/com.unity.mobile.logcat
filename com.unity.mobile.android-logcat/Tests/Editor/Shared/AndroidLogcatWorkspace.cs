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
