using System;
using UnityEditor;
using UnityEngine.TestTools;

namespace Unity.Android.Logcat
{
    class Workspace
    {
        public static bool IsRunningOnYamato()
        {
            return Environment.GetEnvironmentVariable("YAMATO_PROJECT_ID") != null;
        }

        public static string GetAndroidDeviceInfoAvailable()
        {
            var result = Environment.GetEnvironmentVariable("ANDROID_DEVICE_CONNECTION");
            if (result == null)
                return string.Empty;
            return result;
        }
    }
}
