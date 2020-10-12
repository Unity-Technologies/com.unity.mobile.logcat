using NUnit.Framework;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;

using UnityEngine;
using UnityEditor;
using System;

[InitializeOnLoad]
public class AndroidLogcatTestsSetup
{
    public static bool AndroidSDKAndNDKAvailable()
    {
#if UNITY_2019_3_OR_NEWER
        // The only Bokken agents which have android NDK are mobile/android-execution-r19, and those are only Windows currently
        return Application.platform == RuntimePlatform.WindowsEditor;
#else
        return false;
#endif
    }
}
