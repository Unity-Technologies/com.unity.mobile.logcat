using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Android.Logcat;
using Unity.PerformanceTesting;

class AndroidLogcatPerformanceTests
{
    private string m_logMessageByPs = String.Empty;
    private string m_logMessageByDumpsys = String.Empty;

    [SetUp]
    public void SetupLogMessages()
    {
        StreamReader sr = new StreamReader("../../com.unity.mobile.android-logcat/Tests/Editor/LogMessageByShellPS");
        m_logMessageByPs = sr.ReadToEnd();

        sr = new StreamReader("../../com.unity.mobile.android-logcat/Tests/Editor/LogMessageByShellDumpsys");
        m_logMessageByDumpsys = sr.ReadToEnd();
    }

    // Test parsing messages produced by "adb shell ps".
    [PerformanceTest]
    public void ParsePIDByPackageName()
    {
        const int kLoopTime = 20;
        var expectedPid = 26812;

        for (int i = 0; i < kLoopTime; ++i)
        {
            var pid = AndroidLogcatConsoleWindow.ParsePIDInfo("com.samsung.android.app.memo", m_logMessageByPs);
            Assert.IsTrue(pid == expectedPid);
        }
    }

    // Test parsing messages produced by "adb shell "dumpsys activity"".
    [PerformanceTest]
    public void ParseTopActivity()
    {
        const int kLoopTime = 20;
        var expectedPid = 4332;
        var expectedPackageName = "com.sec.android.app.launcher";

        for (int i = 0; i < kLoopTime; ++i)
        {
            string packageName;
            var pid = AndroidLogcatConsoleWindow.ParseTopActivityPackageInfo(m_logMessageByDumpsys, out packageName);
            Assert.IsTrue(pid == expectedPid);
            Assert.IsTrue(packageName == expectedPackageName);
        }
    }
}
