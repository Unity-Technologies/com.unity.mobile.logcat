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

        public static bool IsAndroidDeviceInfoAvailable()
        {
            return Environment.GetEnvironmentVariable("ANDROID_DEVICE_CONNECTION") != null;
        }
    }
}
