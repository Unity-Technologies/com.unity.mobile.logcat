using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using Unity.PerformanceTesting;
using UnityEditor.Android;

internal class AndroidLogcatTestFactory : IAndroidLogcatFactory
{
    public IAndroidLogcatProcess CreateLogcatProcess(ADB adb, bool isAndroid7orAbove, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction)
    {
        return new AndroidLogcatFakeProcess(adb, isAndroid7orAbove, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
    }
}