using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Android.Logcat
{
#if UNITY_6000_1_OR_NEWER
    internal class AndroidLogcatRunCallbacks : IPostprocessLaunch
    {
        public int callbackOrder => 0;

        private void Log(string message)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
        }

        public void OnPostprocessLaunch(ILaunchResult launchResult)
        {
            if (!AndroidLogcatConsoleWindow.ShowDuringBuildRun)
                return;

            if (launchResult.BuildTarget != NamedBuildTarget.Android)
                return;

#if UNITY_ANDROID
            var androidResult = launchResult.AsAndroidResult();
            if (androidResult != null)
            {
                var wnd = AndroidLogcatConsoleWindow.ShowNewOrExisting();
                if (androidResult.Launches.Length > 0)
                    wnd.SetAutoSelect(androidResult.Launches[0].DeviceId, androidResult.Launches[0].PackageName);
            }
#endif
        }
    }
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
