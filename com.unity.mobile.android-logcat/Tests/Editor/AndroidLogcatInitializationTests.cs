using NUnit.Framework;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;
using UnityEngine;


internal class AndroidLogcatInitializationTests : AndroidLogcatRuntimeTestBase
{
    static IAndroidLogcatRuntime ms_Runtime;
    class AndroidLogcatTestConsoleWindow : AndroidLogcatConsoleWindow
    {
        public new void OnEnable()
        {
            OnEnable(ms_Runtime);
        }
    }

    private void InitRuntimeStatic()
    {
        InitRuntime();
        ms_Runtime = m_Runtime;
    }

    private void ShutdownRuntimeStatic()
    {
        ShutdownRuntime();
        ms_Runtime = null;
    }

    /// <summary>
    /// In Unity, ScriptableObject destroy queue order is undefined
    /// This test checks if everything is working correctly, if runtime is destroyed first and last
    /// </summary>
    [Test]
    public void DisableHandledCorrectlyRuntimeDestroyedLast()
    {
        InitRuntimeStatic();
        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic();
    }

    [Test]
    public void DisableHandledCorrectlyRuntimeDestroyedFirst()
    {
        InitRuntimeStatic();
        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();

        ShutdownRuntimeStatic();
        ScriptableObject.DestroyImmediate(consoleWindow);
    }
}
