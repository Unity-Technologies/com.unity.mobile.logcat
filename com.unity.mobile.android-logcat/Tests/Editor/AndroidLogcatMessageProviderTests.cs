using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Android.Logcat;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;

internal class AndroidLogcatFakeMessageProvider : AndroidLogcatMessageProviderBase
{
    private bool m_Started;
    private List<string> m_FakeMessages;
    private Regex m_Regex;

    internal AndroidLogcatFakeMessageProvider(AndroidBridge.ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
        : base(adb, filter, priority, packageID, logPrintFormat, device, logCallbackAction)
    {
        m_FakeMessages = new List<string>();
        m_Started = false;
        if (!string.IsNullOrEmpty(m_Filter))
            m_Regex = new Regex(m_Filter);
    }

    internal Regex LogParseRegex
    {
        get { return m_Device.SupportYearFormat ? AndroidLogcat.m_LogCatEntryYearRegex : AndroidLogcat.m_LogCatEntryThreadTimeRegex; }
    }

    public void SupplyFakeMessage(string message)
    {
        m_FakeMessages.Add(message);
        if (m_Started)
            FlushFakeMessages();
    }

    private void FlushFakeMessages()
    {
        var regex = LogParseRegex;
        foreach (var message in m_FakeMessages)
        {
            if (!string.IsNullOrEmpty(message))
            {
                var m = regex.Match(message);

                // Simulate filtering by PID
                if (m_Device.SupportsFilteringByPid && m_PackageID > 0 && Int32.Parse(m.Groups["pid"].Value) != m_PackageID)
                    continue;

                // Simulate filtering by text
                if (m_Device.SupportsFilteringByRegex && m_Regex != null && !m_Regex.Match(message).Success)
                    continue;
            }

            m_LogCallbackAction(message);
        }
        m_FakeMessages.Clear();
    }

    public override void Start()
    {
        m_Started = true;
        FlushFakeMessages();
    }

    public override void Stop()
    {
        m_Started = false;
    }

    public override void Kill()
    {
    }

    public override bool HasExited
    {
        get
        {
            return false;
        }
    }
}

internal class AndroidLogcatMessagerProvideTests : AndroidLogcatRuntimeTestBase
{
    private static IAndroidLogcatDevice[] kDevices = new IAndroidLogcatDevice[] { new AndroidLogcatFakeDevice60("Fake60"), new AndroidLogcatFakeDevice90("Fake90")};

    private static void SupplyFakeMessages(AndroidLogcatFakeMessageProvider provider, IAndroidLogcatDevice device, string[] messages)
    {
        foreach (var m in messages)
        {
            if (device.APILevel > 23)
                provider.SupplyFakeMessage("1991-" + m);
            else
                provider.SupplyFakeMessage(m);
        }
    }

    [Test]
    public void RegexFilterCorrectlyFormed()
    {
        var filter = ".*abc";
        InitRuntime();

        foreach (var device in kDevices)
        {
            foreach (var isRegexEnabled in new[] {true, false})
            {
                var logcat = new AndroidLogcat(m_Runtime, null, device, -1, AndroidLogcat.Priority.Verbose, ".*abc",
                    isRegexEnabled, new string[] {});
                var message = string.Format("Failure with {0} device, regex enabled: {1}", device.GetType().FullName,
                    isRegexEnabled.ToString());

                if (device.SupportsFilteringByRegex)
                {
                    if (isRegexEnabled)
                        Assert.IsTrue(logcat.Filter.Equals(filter), message);
                    else
                        Assert.IsTrue(logcat.Filter.Equals(Regex.Escape(filter)), message);
                }
                else
                {
                    Assert.IsTrue(logcat.Filter.Equals(filter), message);
                }
            }
        }

        ShutdownRuntime();
    }

    [Test]
    public void ManualRegexFilteringWorks()
    {
        var messages = new[]
        {
            @"10-25 14:27:56.862  2255  2255 I chromium: Help",
            @"10-25 14:27:56.863  2255  2255 I chromium: .abc",
            // Empty lines were reported by devices like LG with Android 5
            @"",
            null
        };

        InitRuntime();
        foreach (var device in kDevices)
        {
            foreach (var regexIsEnabled in new[] {true, false})
            {
                foreach (var filter in new[] {"", ".abc", "...."})
                {
                    var entries = new List<string>();
                    var logcat = new AndroidLogcat(m_Runtime, null, device, -1, AndroidLogcat.Priority.Verbose, filter, regexIsEnabled, new string[] {});
                    logcat.LogEntriesAdded += (List<AndroidLogcat.LogEntry> e) =>
                    {
                        entries.AddRange(e.Select(m => m.message));
                    };
                    logcat.Start();

                    SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, device, messages);

                    m_Runtime.OnUpdate();
                    // We always ignore empty lines
                    Assert.IsFalse(entries.Contains(""));
                    Assert.IsFalse(entries.Contains(null));
                    if (filter == "")
                    {
                        Assert.IsTrue(entries.Contains(".abc"));
                        Assert.IsTrue(entries.Contains("Help"));
                    }
                    else if (filter == ".abc")
                    {
                        Assert.IsTrue(entries.Contains(".abc"));
                        Assert.IsTrue(!entries.Contains("Help"));
                    }
                    else if (filter == "....")
                    {
                        if (regexIsEnabled)
                        {
                            Assert.IsTrue(entries.Contains(".abc"));
                            Assert.IsTrue(entries.Contains("Help"));
                        }
                        else
                        {
                            Assert.IsFalse(entries.Contains(".abc"));
                            Assert.IsFalse(entries.Contains("Help"));
                        }
                    }

                    logcat.Stop();
                }
            }
        }

        ShutdownRuntime();
    }

    [Test]
    public void ManualPidFilteringWorks()
    {
        var messages = new[]
        {
            @"10-25 14:27:56.862  1  2255 I chromium: Help",
            @"10-25 14:27:56.863  2  2255 I chromium: .abc",
            @"10-25 14:27:56.863  3  2255 I chromium: "
        };

        InitRuntime();

        foreach (var device in kDevices)
        {
            foreach (var pid in new[] {-1, 0, 1})
            {
                var processIds = new List<int>();
                var logcat = new AndroidLogcat(m_Runtime, null, device, pid, AndroidLogcat.Priority.Verbose, "", false, new string[] {});
                logcat.LogEntriesAdded += (List<AndroidLogcat.LogEntry> e) =>
                {
                    processIds.AddRange(e.Select(m => m.processId));
                };
                logcat.Start();

                SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, device, messages);

                m_Runtime.OnUpdate();

                switch (pid)
                {
                    // Should accept messages from any process id
                    case -1:
                        Assert.IsTrue(processIds.Contains(1));
                        Assert.IsTrue(processIds.Contains(2));
                        Assert.IsTrue(processIds.Contains(3));
                        break;
                    // Should accept messages from any process id
                    case 0:
                        Assert.IsTrue(processIds.Contains(1));
                        Assert.IsTrue(processIds.Contains(2));
                        Assert.IsTrue(processIds.Contains(3));
                        break;
                    // Should accept messages from process id which equals 1
                    case 1:
                        Assert.IsTrue(processIds.Contains(1));
                        Assert.IsFalse(processIds.Contains(2));
                        Assert.IsFalse(processIds.Contains(3));
                        break;
                }

                logcat.Stop();
            }
        }

        ShutdownRuntime();
    }

    [Test]
    public void MessageProviderForAndroid60DevicesDontAcceptFilter()
    {
        InitRuntime();
        Assert.Throws(typeof(Exception), () =>
            m_Runtime.CreateMessageProvider(null, "Test", AndroidLogcat.Priority.Verbose, -1, "sds", new AndroidLogcatFakeDevice60("Fake60"), null)
        );
        ShutdownRuntime();
    }
}
