#if PLATFORM_ANDROID

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatConnectToDeviceInput : IAndroidLogcatTaskInput
    {
        internal ADB adb;
        internal string ip;
        internal string port;
    }

    internal class AndroidLogcatConnectToDeviceResult : IAndroidLogcatTaskResult
    {
        internal bool success;
        internal string message;
    }

    internal class AndroidLogcatConnectToDeviceTask
    {
        internal static IAndroidLogcatTaskResult Execute(IAndroidLogcatTaskInput input)
        {
            var adb = ((AndroidLogcatConnectToDeviceInput)input).adb;

            if (adb == null)
                throw new NullReferenceException("ADB interface has to be valid");

            var ip = ((AndroidLogcatConnectToDeviceInput)input).ip;
            var port = ((AndroidLogcatConnectToDeviceInput)input).port;
            var cmd = "connect " + ip + ":" + port;
            AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

            var errorMsg = "Unable to connect to ";
            var outputMsg = adb.Run(new[] { cmd }, errorMsg + ip + ":" + port);
            var result = new AndroidLogcatConnectToDeviceResult();
            result.message = outputMsg;
            result.success = true;
            if (outputMsg.StartsWith(errorMsg) || outputMsg.StartsWith("failed to connect"))
            {
                AndroidLogcatInternalLog.Log(outputMsg);
                result.success = false;
            }
            return result;
        }
    }
}
#endif
