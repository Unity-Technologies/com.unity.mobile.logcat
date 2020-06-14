using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;
using UnityEngine;


internal class AndroidLogcatInitializationTests : AndroidLogcatRuntimeTestBase
{
    // We need this for AndroidLogcatTestConsoleWindow
    static AndroidLogcatRuntimeBase ms_Runtime;
    const string kMyCustomTag = "CustomTAG1234";

    class AndroidLogcatTestConsoleWindow : AndroidLogcatConsoleWindow
    {
        public new void OnEnable()
        {
            // Unity implicitly saves all scriptable objects, including AndroidLogcatTestConsoleWindow
            // So during domain reload AndroidLogcatTestConsoleWindow is created and OnEnable is called
            // But we want AndroidLogcatTestConsoleWindow to be available only for tests
            // Not sure how to avoid this initialization better
            if (ms_Runtime == null)
                return;
            OnInternalEnable(ms_Runtime);
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

    private AndroidLogcatTestConsoleWindow StartPlayerSettingsTest()
    {
        InitRuntimeStatic(true);
        Assert.IsFalse(File.Exists(AndroidLogcatTestRuntime.kAndroidLogcatSettingsPath));

        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();
        m_Runtime.ProjectSettings.TagControl.Add(kMyCustomTag);

        return consoleWindow;
    }

    private void StopPlayerSettingsTest()
    {
        // Check if player settings have our new tag saved
        var contents = File.ReadAllText(AndroidLogcatTestRuntime.kAndroidLogcatSettingsPath);
        Assert.IsTrue(contents.Contains(kMyCustomTag));

        // Resume runtime and see if we can restore player settings
        InitRuntimeStatic(false);
        Assert.IsTrue(File.Exists(AndroidLogcatTestRuntime.kAndroidLogcatSettingsPath));
        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();

        Assert.IsTrue(m_Runtime.ProjectSettings.TagControl.Tags.Where(m => m.Name.Equals(kMyCustomTag)).First() != null);

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(true);
    }

    /// <summary>
    /// In Unity, ScriptableObject destroy queue order is undefined
    /// This test checks if everything is working correctly, if runtime is destroyed last and first
    /// </summary>
    [Test]
    public void PlayerSettingsAreSavedWhenRuntimeDestroyedLast()
    {
        var consoleWindow = StartPlayerSettingsTest();

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(false);

        StopPlayerSettingsTest();
    }

    [Test]
    public void PlayerSettingsAreSavedWhenRuntimeDestroyedFirst()
    {
        var consoleWindow = StartPlayerSettingsTest();

        ShutdownRuntimeStatic(false);
        ScriptableObject.DestroyImmediate(consoleWindow);

        StopPlayerSettingsTest();
    }

    AndroidLogcatFakeDeviceQuery PrepareQuery()
    {
        var query = (AndroidLogcatFakeDeviceQuery)m_Runtime.DeviceQuery;
        query.QueueDeviceInfos(@"myandroid1 device
myandroid2 device
");
        query.UpdateConnectedDevicesList(true);
        return query;
    }

    [Test]
    public void SavedSelectedDeviceIsPickedDuringRestart()
    {
        InitRuntimeStatic(true);
        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();
        var query = PrepareQuery();

        // Pretend to be a user and select the device
        query.SelectDevice(query.Devices["myandroid2"]);
        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(false);

        InitRuntimeStatic(false);
        Assert.AreEqual("myandroid2", m_Runtime.ProjectSettings.LastSelectedDeviceId);
        query = PrepareQuery();
        consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();
        // Since the selected device was saved in player settings
        // Console window should auto select it
        m_Runtime.OnUpdate();
        Assert.AreEqual(query.Devices["myandroid2"], query.SelectedDevice);

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(true);
    }

    [Test]
    public void SavedSelectedPackageIsPickedDuringRestart()
    {
        InitRuntimeStatic(true);
        var consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();
        var query = PrepareQuery();

        // Pretend to be a user and select the device
        query.SelectDevice(query.Devices["myandroid2"]);

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(false);

        InitRuntimeStatic(false);
        Assert.AreEqual("myandroid2", m_Runtime.ProjectSettings.LastSelectedDeviceId);
        query = PrepareQuery();
        consoleWindow = AndroidLogcatTestConsoleWindow.CreateInstance<AndroidLogcatTestConsoleWindow>();
        // Since the selected device was saved in player settings
        // Console window should auto select it
        m_Runtime.OnUpdate();
        Assert.AreEqual(query.Devices["myandroid2"], query.SelectedDevice);

        ScriptableObject.DestroyImmediate(consoleWindow);
        ShutdownRuntimeStatic(true);
    }
}
