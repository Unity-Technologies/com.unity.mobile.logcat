using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;

internal class AndroidLogcatTestRuntime : AndroidLogcatRuntimeBase
{
    internal static readonly string kUserSettingsPath = Path.Combine("Tests", "UserSettings", "AndroidLogcatSettings.asset");

    protected override string UserSettingsPath { get => kUserSettingsPath; }

    public override AndroidLogcatMessageProviderBase CreateMessageProvider(AndroidBridge.ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
    {
        return new AndroidLogcatFakeMessageProvider(adb, filter, priority, packageID, logPrintFormat, device, logCallbackAction);
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

    protected override void SaveEditorSettings(AndroidLogcatSettings settings)
    {
        // Don't save editor settings for tests
    }
}

internal class AndroidLogcatRuntimeTestBase
{
    protected AndroidLogcatTestRuntime m_Runtime;

    protected void Cleanup()
    {
        if (Directory.Exists("Tests"))
            Directory.Delete("Tests", true);
    }

    protected void InitRuntime(bool cleanup = true)
    {
        if (m_Runtime != null)
            throw new Exception("Runtime was not shutdown by previous test?");
        m_Runtime = new AndroidLogcatTestRuntime();
        if (cleanup)
            Cleanup();
        m_Runtime.Initialize();
    }

    protected void ShutdownRuntime(bool cleanup = true)
    {
        if (m_Runtime == null)
            throw new Exception("Runtime was not created?");
        m_Runtime.Shutdown();
        if (cleanup)
            Cleanup();
        m_Runtime = null;
    }
}
