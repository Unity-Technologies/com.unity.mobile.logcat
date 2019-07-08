#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;


namespace Unity.Android.Logcat
{
    internal interface IAndroidLogcatFactory
    {
        IAndroidLogcatProcess CreateLogcatProcess(ADB adb, bool isAndroid7orAbove, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction);
    }

    internal class AndroidLogcatFactory : IAndroidLogcatFactory
    {
        public IAndroidLogcatProcess CreateLogcatProcess(ADB adb, bool isAndroid7orAbove, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction)
        {
            return new AndroidLogcatProcess(adb, isAndroid7orAbove, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
        }
    }


}
#endif
