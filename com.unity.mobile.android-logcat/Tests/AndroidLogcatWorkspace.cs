using System;
using UnityEditor;
using UnityEngine.TestTools;

namespace Unity.Android.Logcat
{
    public class Workspace
    {
        public static bool IsRunningOnYamato()
        {
            return Environment.GetEnvironmentVariable("YAMATO_PROJECT_ID") != null;
        }

        public static string GetAndroidDeviceInfo()
        {
            var result = Environment.GetEnvironmentVariable("ANDROID_DEVICE_CONNECTION");
            if (result == null)
                return string.Empty;
            return result;
        }
    }
}
