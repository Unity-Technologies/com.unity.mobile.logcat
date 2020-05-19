using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor.Android;

internal class AndroidLogcatTestRuntime : IAndroidLogcatRuntime
{
    internal static readonly string kAndroidLogcatSettingsPath = Path.Combine("Tests", "ProjectSettings", "AndroidLogcatSettings.asset");

    private AndroidLogcatDispatcher m_Dispatcher;
    private AndroidLogcatSettings m_Settings;
    private AndroidLogcatProjectSettings m_ProjectSettings;
    private AndroidLogcatFakeDeviceQuery m_DeviceQuery;
    private bool m_Initialized;

    public event Action Update;
    public event Action Closing;

    public IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction)
    {
        return new AndroidLogcatFakeMessageProvider(adb, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
    }

    private void ValidateIsInitialized()
    {
        if (!m_Initialized)
            throw new Exception("Runtime is not initialized");
    }

    public AndroidLogcatDispatcher Dispatcher
    {
        get { ValidateIsInitialized(); return m_Dispatcher; }
    }

    public AndroidLogcatSettings Settings
    {
        get { ValidateIsInitialized(); return m_Settings;  }
    }

    public AndroidLogcatProjectSettings ProjectSettings
    {
        get { ValidateIsInitialized(); return m_ProjectSettings; }
    }

    public AndroidTools Tools
    {
        get { ValidateIsInitialized(); return null; }
    }

    public AndroidLogcatDeviceQueryBase DeviceQuery
    {
        get { ValidateIsInitialized(); return m_DeviceQuery; }
    }

    public void Cleanup()
    {
        Directory.Delete("Tests", true);
    }

    public void Initialize()
    {
        m_Dispatcher = new AndroidLogcatDispatcher(this);
        m_Dispatcher.Initialize();

        m_Settings = new AndroidLogcatSettings();

        Directory.CreateDirectory(Path.GetDirectoryName(kAndroidLogcatSettingsPath));
        m_ProjectSettings = AndroidLogcatProjectSettings.Load(kAndroidLogcatSettingsPath);
        if (m_ProjectSettings == null)
        {
            m_ProjectSettings = new AndroidLogcatProjectSettings();
            m_ProjectSettings.Reset();
        }

        m_DeviceQuery = new AndroidLogcatFakeDeviceQuery(this);
        m_Initialized = true;
    }

    public void Shutdown()
    {
        Closing?.Invoke();
        m_Initialized = false;

        m_DeviceQuery = null;

        AndroidLogcatProjectSettings.Save(m_ProjectSettings, kAndroidLogcatSettingsPath);
        m_ProjectSettings = null;

        m_Dispatcher.Shutdown();
        m_Dispatcher = null;
    }

    /// <summary>
    /// Should be called manually from the test
    /// </summary>
    public void OnUpdate()
    {
        Update?.Invoke();
    }
}


internal class AndroidLogcatRuntimeTestBase
{
    protected AndroidLogcatTestRuntime m_Runtime;

    protected void InitRuntime(bool cleanup = true)
    {
        if (m_Runtime != null)
            throw new Exception("Runtime was not shutdown by previous test?");
        m_Runtime = new AndroidLogcatTestRuntime();
        if (cleanup)
            m_Runtime.Cleanup();
        m_Runtime.Initialize();
    }

    protected void ShutdownRuntime()
    {
        if (m_Runtime == null)
            throw new Exception("Runtime was not created?");
        m_Runtime.Shutdown();
        m_Runtime = null;
    }
}
