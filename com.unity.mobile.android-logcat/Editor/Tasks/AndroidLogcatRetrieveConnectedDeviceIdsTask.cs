#if PLATFORM_ANDROID

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatRetrieveDeviceIdsInput : AndroidLogcatTaskInput
    { 
        internal ADB adb;
    }

    internal class AndroidLogcatRetrieveDeviceIdsResult : AndroidLogcatTaskResult
    {
        internal List<string> deviceIds = new List<string>();
    }

    internal class AndroidLogcatRetrieveDeviceIdsTask
    {
        internal static AndroidLogcatTaskResult Execute(AndroidLogcatTaskInput input)
        {
            var adb = ((AndroidLogcatRetrieveDeviceIdsInput)input).adb;

            var result = new AndroidLogcatRetrieveDeviceIdsResult();

            AndroidLogcatInternalLog.Log("{0} devices", adb.GetADBPath());
            var adbOutput = adb.Run(new[] { "devices" }, "Unable to list connected devices. ");
            foreach (var line in adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()))
            {
                AndroidLogcatInternalLog.Log(" " + line);
                if (line.EndsWith("device"))
                {
                    var deviceId = line.Substring(0, line.IndexOf('\t'));
                    result.deviceIds.Add(deviceId);
                }
            }

            return result;

        }
    }
}   
#endif
