using NUnit.Framework;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;

using UnityEngine;
using UnityEditor;
using System;

[InitializeOnLoad]
public class AndroidLogcatTestsSetup
{
    /// <summary>
    /// Set SKD/NDK for Bokken images
    /// </summary>
    static AndroidLogcatTestsSetup()
    {
#if UNITY_2019_3_OR_NEWER
        if (!AndroidBridge.AndroidExtensionsInstalled)
        {
            Debug.LogWarning("Android Support not installed, skipping setup.");
            return;
        }

        var sdkPath = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if (sdkPath != string.Empty)
        {
            AndroidBridge.AndroidExternalToolsSettings.sdkRootPath = sdkPath;
            Debug.Log($"SDK Path was set from ANDROID_SDK_ROOT = {sdkPath}");
        }
        else
        {
            Debug.LogWarning($"ANDROID_SDK_ROOT was not set.\nCurrently using SDK from here: {AndroidBridge.AndroidExternalToolsSettings.sdkRootPath}");
        }

        var ndkPath = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
        if (ndkPath != string.Empty)
        {
            AndroidBridge.AndroidExternalToolsSettings.ndkRootPath = ndkPath;
            Debug.Log($"SDK Path was set from ANDROID_NDK_ROOT = {ndkPath}");
        }
        else
        {
            Debug.LogWarning($"ANDROID_NDK_ROOT was not set.\nCurrently using NDK from here: {AndroidBridge.AndroidExternalToolsSettings.ndkRootPath}");
        }
#endif
    }

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
