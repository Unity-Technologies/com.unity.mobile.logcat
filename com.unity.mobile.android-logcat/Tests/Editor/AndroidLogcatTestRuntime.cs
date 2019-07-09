using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using Unity.PerformanceTesting;
using UnityEditor.Android;

internal class AndroidLogcatTestRuntime : IAndroidLogcatRuntime
{
    private AndroidLogcatDispatcher m_Dispatcher;

    public event Action OnUpdate;

    public IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, bool isAndroid7orAbove, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction)
    {
        return new AndroidLogcatFakeProcess(adb, isAndroid7orAbove, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
    }

    public AndroidLogcatDispatcher Dispatcher
    {
        get { return m_Dispatcher; }
    }

    public void Initialize()
    {
        m_Dispatcher = new AndroidLogcatDispatcher(this);
        m_Dispatcher.Initialize();
    }

    public void Shutdown()
    {
        m_Dispatcher.Shutdown();
        m_Dispatcher = null;
    }

    /// <summary>
    /// Should be called manually from the test
    /// </summary>
    public void Update()
    {
        OnUpdate?.Invoke();
    }
}
