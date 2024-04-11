using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;

internal class AndroidLogcatTestRuntime : AndroidLogcatRuntimeBase
{
    internal static readonly string kUserSettingsPath = Path.Combine("Tests", "UserSettings", "AndroidLogcatSettings.asset");

    protected override string UserSettingsPath { get => kUserSettingsPath; }

    public override AndroidLogcatMessageProviderBase CreateMessageProvider(AndroidBridge.ADB adb, Priority priority, int packageID, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
    {
        return new AndroidLogcatFakeMessageProvider(adb, priority, packageID, logPrintFormat, device, logCallbackAction);
    }

    protected override AndroidTools CreateAndroidTools()
    {
        // Mac agents don't have SDK/NDK set up, for now return null for AndroidTools and make code work even if there's no Tools
        return null;
    }

    protected override AndroidLogcatDeviceQueryBase CreateDeviceQuery()
    {
        return new AndroidLogcatFakeDeviceQuery(this);
    }

    protected override AndroidLogcatSettings LoadEditorSettings()
    {
        return new AndroidLogcatSettings();
    }

    protected override AndroidLogcatCaptureVideo CreateScreenRecorder()
    {
        return null;
    }
    protected override AndroidLogcatCaptureScreenshot CreateScreenCapture()
    {
        return null;
    }

    protected override void SaveEditorSettings(AndroidLogcatSettings settings)
    {
        // Don't save editor settings for tests
    }
}

internal class AndroidLogcatRuntimeTestBase
{
    protected AndroidLogcatTestRuntime m_Runtime;

    protected class AutoRuntime : IDisposable
    {
        AndroidLogcatRuntimeTestBase m_Parent;
        bool m_CleanUp;
        public AutoRuntime(AndroidLogcatRuntimeTestBase parent, bool cleanup = true)
        {
            m_Parent = parent;
            m_CleanUp = cleanup;
            m_Parent.InitRuntime(m_CleanUp);
        }

        public void Dispose()
        {
            m_Parent.ShutdownRuntime(m_CleanUp);
        }
    }

    protected void Cleanup()
    {
        if (Directory.Exists("Tests"))
            Directory.Delete("Tests", true);
    }

    protected void InitRuntime(bool cleanup)
    {
        if (m_Runtime != null)
            throw new Exception("Runtime was not shutdown by previous test?");
        m_Runtime = new AndroidLogcatTestRuntime();
        if (cleanup)
            Cleanup();
        m_Runtime.Initialize();
    }

    protected void ShutdownRuntime(bool cleanup)
    {
        if (m_Runtime == null)
            throw new Exception("Runtime was not created?");
        m_Runtime.Shutdown();
        if (cleanup)
            Cleanup();
        m_Runtime = null;
    }
}
