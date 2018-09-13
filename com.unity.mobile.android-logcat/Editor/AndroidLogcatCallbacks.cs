using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCallbacks : IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.options.HasFlag(BuildOptions.AutoRunPlayer) && AndroidLogcatConsoleWindow.ShowDuringBuildRun)
                AndroidLogcatConsoleWindow.ShowNewOrExisting(true);
        }
    }
}
