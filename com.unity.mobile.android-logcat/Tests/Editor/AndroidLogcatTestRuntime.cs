using System;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor.Android;

internal class AndroidLogcatTestRuntime : IAndroidLogcatRuntime
{
    private AndroidLogcatDispatcher m_Dispatcher;

    public event Action OnUpdate;

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
        if (OnUpdate != null)
            OnUpdate.Invoke();
    }
}
