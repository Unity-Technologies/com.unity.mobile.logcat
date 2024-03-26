using System.Collections.Generic;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal abstract class IAndroidLogcatActivityManager
    {
        internal virtual void StartOrResumePackage(string packageName, string activityName = null) { }

        internal virtual void StopPackage(string packageName) { }

        internal virtual void CrashPackage(string packageName) { }
    }

    internal class AndroidLogcatActivityManager : IAndroidLogcatActivityManager
    {
        AndroidBridge.ADB m_ADB;
        string m_DeviceId;
        internal AndroidLogcatActivityManager(AndroidBridge.ADB adb, string deviceId)
        {
            m_ADB = adb;
            m_DeviceId = deviceId;
        }

        internal override void StartOrResumePackage(string packageName, string activityName = null)
        {
            var args = new List<string>();
            args.AddRange(new[]
            {
                "-s",
                m_DeviceId,
                "shell",
             });

            if (activityName == null)
            {
                args.AddRange(new[]
                {
                    "monkey",
                    $"-p {packageName}",
                    "-c android.intent.category.LAUNCHER 1"
                 });
            }
            else
            {
                args.AddRange(new[]
                {
                    "am",
                    "start",
                    $"-n \"{packageName}/{activityName}\""
                });
            }

            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args.ToArray(), $"Failed to start package '{packageName}'");
        }

        internal override void StopPackage(string packageName)
        {
            var args = new[]
            {
                "-s",
                m_DeviceId,
                "shell",
                "am",
                "force-stop",
                packageName
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to stop package '{packageName}'");
        }

        internal override void CrashPackage(string packageName)
        {
            var args = new[]
            {
                "-s",
                m_DeviceId,
                "shell",
                "am",
                "crash",
                packageName
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to crash package '{packageName}'");
        }
    }
}
