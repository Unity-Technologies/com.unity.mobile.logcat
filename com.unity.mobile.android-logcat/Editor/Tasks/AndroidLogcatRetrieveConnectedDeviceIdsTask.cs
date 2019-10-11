#if PLATFORM_ANDROID

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatRetrieveDeviceIdsInput : IAndroidLogcatTaskInput
    {
        internal ADB adb;
    }

    internal class AndroidLogcatRetrieveDeviceIdsResult : IAndroidLogcatTaskResult
    {
        internal List<string> deviceIds = new List<string>();
    }

    internal class AndroidLogcatRetrieveDeviceIdsTask
    {
        internal static IAndroidLogcatTaskResult Execute(IAndroidLogcatTaskInput input)
        {
            var adb = ((AndroidLogcatRetrieveDeviceIdsInput)input).adb;

            if (adb == null)
                throw new NullReferenceException("ADB interface has to be valid");

            var result = new AndroidLogcatRetrieveDeviceIdsResult();

            AndroidLogcatInternalLog.Log("{0} devices", adb.GetADBPath());
            var adbOutput = adb.Run(new[] { "devices" }, "Unable to list connected devices. ");
            foreach (var line in adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()))
            {
                AndroidLogcatInternalLog.Log(" " + line);
                if (line.EndsWith("device"))
                {
                    var deviceId = line.Split(new[] { '\t', ' ' })[0];
                    result.deviceIds.Add(deviceId);
                }
            }

            return result;
        }
    }
}
#endif
