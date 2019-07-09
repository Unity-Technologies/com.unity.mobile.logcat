using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor.Android;
using System.Collections.Generic;
using UnityEngine.TestTools;

internal class AndroidLogcatFakeProcess : IAndroidLogcatMessageProvider
{
    private ADB m_ADB;
    private bool m_IsAndroid7OrAbove;
    private string m_Filter;
    private AndroidLogcat.Priority m_Priority;
    private int m_PackageID;
    private string m_LogPrintFormat;
    private string m_DeviceId;
    private Action<string> m_LogCallbackAction;
    private bool m_Started;

    private List<string> m_FakeMessages;

    internal AndroidLogcatFakeProcess(ADB adb, bool isAndroid7orAbove, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction)
    {
        m_ADB = adb;
        m_IsAndroid7OrAbove = isAndroid7orAbove;
        m_Filter = filter;
        m_Priority = priority;
        m_PackageID = packageID;
        m_LogPrintFormat = logPrintFormat;
        m_DeviceId = deviceId;
        m_LogCallbackAction = logCallbackAction;

        m_FakeMessages = new List<string>();
        m_Started = false;
    }

    public void SupplyFakeMessage(string message)
    {
        m_FakeMessages.Add(message);
        if (m_Started)
            FlushFakeMessages();
    }

    private void FlushFakeMessages()
    {
        foreach (var m in m_FakeMessages)
        {
            m_LogCallbackAction(m);
        }
        m_FakeMessages.Clear();
    }

    public void Start()
    {
        m_Started = true;
        FlushFakeMessages();
    }

    public void Stop()
    {
        m_Started = false;
    }

    public void Kill()
    {
    }

    public bool HasExited
    {
        get
        {
            return false;
        }
    }
}

internal class AndroidLogcatProcessTests
{
    [UnityTest]
    public IEnumerator MessagesAreFilteredCorrectly()
    {
        var runtime = new AndroidLogcatTestRuntime();
        runtime.Initialize();
        var entries = new List<AndroidLogcat.LogEntry>();
        var logcat = new AndroidLogcat(runtime, null, null, 28, -1, AndroidLogcat.Priority.Verbose, "", false, new string[] {});
        logcat.LogEntriesAdded += (List<AndroidLogcat.LogEntry> e) => { entries.AddRange(e); };
        logcat.Start();

        var process = (AndroidLogcatFakeProcess)logcat.Process;
        process.SupplyFakeMessage("Test");

        yield return null;

        logcat.Stop();

        foreach (var e in entries)
        {
            UnityEngine.Debug.Log(e.message);
        }
        runtime.Shutdown();
    }
}
