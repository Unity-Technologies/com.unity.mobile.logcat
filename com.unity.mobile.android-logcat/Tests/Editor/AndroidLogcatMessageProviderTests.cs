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

    internal AndroidLogcatFakeMessageProvider(AndroidBridge.ADB adb, Priority priority, int packageID, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
        : base(adb, priority, packageID, logPrintFormat, device, logCallbackAction)
    {
        m_FakeMessages = new List<string>();
        m_Started = false;
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
    private static IAndroidLogcatDevice kDefaultDevice = new AndroidLogcatFakeDevice90("Fake90");
    private static IAndroidLogcatDevice[] kDevices = new IAndroidLogcatDevice[] { new AndroidLogcatFakeDevice60("Fake60"), kDefaultDevice };

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
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(false, true)]
    public void FilteringWorks(bool useRegularExpressions, bool matchCase)
    {
        var messages = new[]
        {
            @"10-25 14:27:56.862  2255  2255 I chromium: Help",
            @"10-25 14:27:56.863  2255  2255 I chromium: .abc",
            // Empty lines were reported by devices like LG with Android 5
            @"",
            null
        };

        var checks = new KeyValuePair<string, Action<IReadOnlyList<string>>>[]
        {
            new KeyValuePair<string, Action<IReadOnlyList<string>>>
            (
                "",
                new Action<IReadOnlyList<string>>((entries) =>
                {
                    Assert.IsTrue(entries.Contains(".abc"));
                    Assert.IsTrue(entries.Contains("Help"));
                })
            ),


            new KeyValuePair<string, Action<IReadOnlyList<string>>>
            (
                ".*abc",
                new Action<IReadOnlyList<string>>((entries) =>
                {
                    if (useRegularExpressions)
                    {
                        Assert.IsTrue(entries.Contains(".abc"));
                        Assert.IsFalse(entries.Contains("Help"));
                    }
                    else
                    {
                        Assert.IsFalse(entries.Contains(".abc"));
                        Assert.IsFalse(entries.Contains("Help"));
                    }
                })
            ),

            new KeyValuePair<string, Action<IReadOnlyList<string>>>
            (
                ".abc",
                new Action<IReadOnlyList<string>>((entries) =>
                {
                    Assert.IsTrue(entries.Contains(".abc"));
                    Assert.IsFalse(entries.Contains("Help"));
                })
            ),

            new KeyValuePair<string, Action<IReadOnlyList<string>>>
            (
                "....",
                new Action<IReadOnlyList<string>>((entries) =>
                {
                    if (useRegularExpressions)
                    {
                        Assert.IsTrue(entries.Contains(".abc"));
                        Assert.IsTrue(entries.Contains("Help"));
                    }
                    else
                    {
                        Assert.IsFalse(entries.Contains(".abc"));
                        Assert.IsFalse(entries.Contains("Help"));
                    }
                })
            ),

            new KeyValuePair<string, Action<IReadOnlyList<string>>>
            (
                ".*ABC",
                new Action<IReadOnlyList<string>>((entries) =>
                {
                    if (useRegularExpressions)
                    {
                        if (matchCase)
                        {
                            Assert.IsFalse(entries.Contains(".abc"));
                            Assert.IsFalse(entries.Contains("Help"));
                        }
                        else
                        {
                            Assert.IsTrue(entries.Contains(".abc"));
                            Assert.IsFalse(entries.Contains("Help"));
                        }
                    }
                    else
                    {
                        Assert.IsFalse(entries.Contains(".abc"));
                        Assert.IsFalse(entries.Contains("Help"));
                    }
                })
            ),

            new KeyValuePair<string, Action<IReadOnlyList<string>>>
            (
                ".ABC",
                new Action<IReadOnlyList<string>>((entries) =>
                {

                    if (matchCase)
                    {
                        Assert.IsFalse(entries.Contains(".abc"));
                        Assert.IsFalse(entries.Contains("Help"));
                    }
                    else
                    {
                        Assert.IsTrue(entries.Contains(".abc"));
                        Assert.IsFalse(entries.Contains("Help"));
                    }
                })
            ),
        };

        InitRuntime();
        foreach (var device in kDevices)
        {
            foreach (var check in checks)
            {
                var logcat = new AndroidLogcat(m_Runtime, null, device, -1, Priority.Verbose,
                    new FilterOptions
                    {
                        Filter = check.Key,
                        UseRegularExpressions = useRegularExpressions,
                        MatchCase = matchCase
                    }, new string[] { });
                logcat.Start();

                SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, device, messages);

                m_Runtime.OnUpdate();
                var entries = logcat.FilteredEntries.Select(e => e.message).ToList();
                // We always ignore empty lines
                Assert.IsFalse(entries.Contains(""));
                Assert.IsFalse(entries.Contains(null));

                check.Value(entries);

                logcat.Stop();

                // Logcat was stopped, check that our filter still works
                check.Value(entries);
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
            foreach (var pid in new[] { -1, 0, 1 })
            {
                var processIds = new List<int>();
                var logcat = new AndroidLogcat(m_Runtime, null, device, pid, Priority.Verbose,
                    new FilterOptions
                    {
                        Filter = "",
                        UseRegularExpressions = false
                    }, new string[] { });
                logcat.FilteredLogEntriesAdded += (IReadOnlyList<LogcatEntry> e) =>
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
    public void MessagesSettingsWork()
    {
        var messages = new[]
        {
            @"10-25 14:27:56.862  1  2255 I chromium: Help",
            @"10-25 14:27:56.863  2  2255 I chromium: .abc",
            @"10-25 14:27:56.863  3  2255 I chromium: "
        };

        InitRuntime();

        m_Runtime.Settings.MaxUnfilteredMessageCount = 20;
        m_Runtime.Settings.MaxFilteredMessageCount = 20;
        var logcat = new AndroidLogcat(m_Runtime, null, kDefaultDevice, -1, Priority.Verbose, new FilterOptions(), new string[] { });
        logcat.Start();

        for (int i = 0; i < 30; i++)
            SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice,
                new[] { $"10-25 14:27:56.862  1  2255 I chromium: {i}" });

        m_Runtime.OnUpdate();

        Assert.AreEqual(20, logcat.FilteredEntries.Count);
        Assert.AreEqual(20, logcat.RawEntries.Count);

        // The first messages are removed (With string from 0 to 9)
        Assert.AreEqual(10.ToString(), logcat.FilteredEntries[0].message);
        Assert.AreEqual(10.ToString(), logcat.RawEntries[0].message);

        m_Runtime.Settings.MaxFilteredMessageCount = 10;
        logcat.ValidateRawEntries();
        logcat.ValidateFilteredEntries();
        Assert.AreEqual(10, logcat.FilteredEntries.Count);
        Assert.AreEqual(20, logcat.RawEntries.Count);
        Assert.AreEqual(20.ToString(), logcat.FilteredEntries[0].message);

        // Remove limiters and see that no messages are dropped
        m_Runtime.Settings.MaxUnfilteredMessageCount = 0;
        m_Runtime.Settings.MaxFilteredMessageCount = 0;
        for (int i = 0; i < 30; i++)
            SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice,
                new[] { $"10-25 14:27:56.862  1  2255 I chromium: {i + 30}" });

        Assert.AreEqual(40, logcat.FilteredEntries.Count);
        Assert.AreEqual(50, logcat.RawEntries.Count);

        logcat.Stop();

        ShutdownRuntime();
    }
}
