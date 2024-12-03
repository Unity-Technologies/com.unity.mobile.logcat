using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Android.Logcat
{
#if UNITY_6000_1_OR_NEWER

#if UNITY_ANDROID
    internal class AndroidLogcatRunCallbacks : IPostprocessLaunch
    {
        public int callbackOrder => 0;

        public void OnPostprocessLaunch(ILaunchReport launchReport)
        {
            if (!AndroidLogcatConsoleWindow.ShowDuringBuildRun)
                return;

            if (launchReport.buildTarget != NamedBuildTarget.Android)
                return;

            var androidReport = launchReport.AsAndroidReport();
            if (androidReport != null)
            {
                var wnd = AndroidLogcatConsoleWindow.ShowNewOrExisting();
                foreach (var l in androidReport.Launches)
                {
                    if (!l.Success)
                        continue;
                    wnd.SetAutoSelect(l.DeviceId, l.PackageName);
                    break;
                }
            }
        }
    }
#endif

#else
    internal class AndroidLogcatCallbacks : IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPostprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.AutoRunPlayer) != 0 &&
                report.summary.platform == BuildTarget.Android &&
                AndroidLogcatConsoleWindow.ShowDuringBuildRun)
            {
                var wnd = AndroidLogcatConsoleWindow.ShowNewOrExisting();
                wnd.SetAutoSelect(string.Empty, string.Empty);
            }
        }
    }
#endif

}
