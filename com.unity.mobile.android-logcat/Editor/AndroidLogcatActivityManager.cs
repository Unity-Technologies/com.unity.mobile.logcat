using System.Collections.Generic;

namespace Unity.Android.Logcat
{
    internal abstract class IAndroidLogcatActivityManager
    {
        internal virtual void StartOrResumePackage(string packageName, string activityName = null) { }
        internal virtual void StopPackage(string packageName) { }
        internal virtual void StopProcess(int processId) { }
        internal virtual void CrashPackage(string packageName) { }
        internal virtual void CrashProcess(int processId) { }
        internal virtual void SendTrimMemory(int processId, AndroidLogcatSendTrimMemoryUsage usage) { }
    }

    /// <summary>
    /// Expose Activity Manager commands
    /// For full list do adb shell am help
    /// </summary>
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

        internal override void StopProcess(int processId)
        {
            var packageName = AndroidLogcatUtilities.GetProcessNameFromPid(m_ADB, m_DeviceId, processId);
            if (!string.IsNullOrEmpty(packageName))
                StopPackage(packageName);
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

        internal override void CrashProcess(int processId)
        {
            var args = new[]
            {
                "-s",
                m_DeviceId,
                "shell",
                "am",
                "crash",
                processId.ToString()
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to crash process '{processId}'");
        }

        internal override void SendTrimMemory(int processId, AndroidLogcatSendTrimMemoryUsage usage)
        {
            var args = new[]
            {
                "-s",
                m_DeviceId,
                "shell",
                "am",
                "send-trim-memory",
                processId.ToString(),
                usage.Value
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to send-trim-memory {processId} {usage.Value}");
        }
    }
}
