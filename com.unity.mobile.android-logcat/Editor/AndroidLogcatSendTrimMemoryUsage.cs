using System.Collections.Generic;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatSendTrimMemoryUsage
    {
        public string DisplayName { get; }
        public string Value { get; }
        AndroidLogcatSendTrimMemoryUsage(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }

        internal static readonly AndroidLogcatSendTrimMemoryUsage Hidden = new AndroidLogcatSendTrimMemoryUsage("Hidden", "HIDDEN");
        internal static readonly AndroidLogcatSendTrimMemoryUsage RunningModerate = new AndroidLogcatSendTrimMemoryUsage("Running Moderate", "RUNNING_MODERATE");
        internal static readonly AndroidLogcatSendTrimMemoryUsage Background = new AndroidLogcatSendTrimMemoryUsage("Background", "BACKGROUND");
        internal static readonly AndroidLogcatSendTrimMemoryUsage RunningLow = new AndroidLogcatSendTrimMemoryUsage("Running Low", "RUNNING_LOW");
        internal static readonly AndroidLogcatSendTrimMemoryUsage Moderate = new AndroidLogcatSendTrimMemoryUsage("Moderate", "MODERATE");
        internal static readonly AndroidLogcatSendTrimMemoryUsage RunningCritical = new AndroidLogcatSendTrimMemoryUsage("Running Critical", "RUNNING_CRITICAL");
        internal static readonly AndroidLogcatSendTrimMemoryUsage Complete = new AndroidLogcatSendTrimMemoryUsage("Complete", "COMPLETE");
        internal static readonly AndroidLogcatSendTrimMemoryUsage[] All = new[]
        {
            Hidden, RunningModerate, Background, RunningLow, Moderate, RunningCritical, Complete
        };
    }
}
