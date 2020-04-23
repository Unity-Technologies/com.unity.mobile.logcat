using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor.Android;

internal class AndroidLogcatTestRuntime : IAndroidLogcatRuntime
{
    private AndroidLogcatDispatcher m_Dispatcher;
    private AndroidLogcatFakeDeviceQuery m_DeviceQuery;

    public event Action Update;

    public IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction)
    {
        return new AndroidLogcatFakeMessageProvider(adb, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
    }

    public AndroidLogcatDispatcher Dispatcher
    {
        get { return m_Dispatcher; }
    }

    public AndroidLogcatSettings Settings
    {
        get { return null;  }
    }

    public AndroidTools Tools
    {
        get { return null; }
    }

    public AndroidLogcatDeviceQueryBase DeviceQuery
    {
        get { return m_DeviceQuery; }
    }

    public void Initialize()
    {
        m_Dispatcher = new AndroidLogcatDispatcher(this);
        m_Dispatcher.Initialize();

        m_DeviceQuery = new AndroidLogcatFakeDeviceQuery(this);
    }

    public void Shutdown()
    {
        m_DeviceQuery = null;

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

    protected void InitRuntime()
    {
        if (m_Runtime != null)
            throw new Exception("Runtime was not shutdown by previous test?");
        m_Runtime = new AndroidLogcatTestRuntime();
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
