using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;
using UnityEngine;


internal class AndroidLogcatInitializationTests : AndroidLogcatRuntimeTestBase
{
    static IAndroidLogcatRuntime ms_Runtime;
    const string kMyCustomTag = "CustomTAG1234";

    class AndroidLogcatTestConsoleWindow : AndroidLogcatConsoleWindow
    {
        public new void OnEnable()
        {
            OnEnable(ms_Runtime);
        }
    }

    private void InitRuntimeStatic(bool cleanup)
    {
        InitRuntime(cleanup);
        ms_Runtime = m_Runtime;
    }

    private void ShutdownRuntimeStatic(bool cleanup)
    {
        ShutdownRuntime(cleanup);
        ms_Runtime = null;
    }

    private AndroidLogcatTestConsoleWindow StartTest()
    {
        InitRuntimeStatic(true);
        Assert.IsFalse(File.Exists(AndroidLogcatTestRuntime.kAndroidLogcatSettingsPath));

        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();
        m_Runtime.ProjectSettings.TagControl.Add(kMyCustomTag);

        return consoleWindow;
    }

    private void FinalizeTest()
    {
        // Check if player settings have our new tag saved
        var contents = File.ReadAllText(AndroidLogcatTestRuntime.kAndroidLogcatSettingsPath);
        Assert.IsTrue(contents.Contains(kMyCustomTag));

        // Resume runtime and see if we can restore player settings
        InitRuntimeStatic(false);
        Assert.IsTrue(File.Exists(AndroidLogcatTestRuntime.kAndroidLogcatSettingsPath));
        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();

        Assert.IsTrue(m_Runtime.ProjectSettings.TagControl.TagNames.Contains(kMyCustomTag));

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(true);
    }

    /// <summary>
    /// In Unity, ScriptableObject destroy queue order is undefined
    /// This test checks if everything is working correctly, if runtime is destroyed last and first
    /// </summary>
    [Test]
    public void LogcatBehavesCorrectlyWhenRuntimeDestroyedLast()
    {
        var consoleWindow = StartTest();

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(false);

        FinalizeTest();
    }

    [Test]
    public void LogcatBehavesCorrectlyWhenRuntimeDestroyedFirst()
    {
        var consoleWindow = StartTest();

        ShutdownRuntimeStatic(false);
        ScriptableObject.DestroyImmediate(consoleWindow);

        FinalizeTest();
    }
}
