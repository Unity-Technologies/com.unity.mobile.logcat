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
            if (m == null)
                provider.SupplyFakeMessage(null);
            else if (device.APILevel > 23)
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
    public void FilteringCanReuseResults()
    {
        var messages = new[]
{
            @"10-25 14:27:56.862  2255  2255 I chromium: abc",
            @"10-25 14:27:56.863  2255  2255 I chromium: abcd",
            @"10-25 14:27:56.863  2255  2255 I chromium: abcde",
            @"10-25 14:27:56.863  2255  2255 I chromium: ABCDE",
        };

        InitRuntime();

        var logcat = new AndroidLogcat(m_Runtime, null, kDefaultDevice, -1, Priority.Verbose,
            new FilterOptions() { UseRegularExpressions = false, MatchCase = false },
            new string[] { });

        var previousFilterChangedCallback = logcat.FilterOptions.OnFilterChanged;
        var filteredResultsWereReused = false;

        logcat.FilterOptions.OnFilterChanged = () =>
        {
            filteredResultsWereReused = logcat.CanReuseFilteredResults();
            previousFilterChangedCallback();
        };

        logcat.Start();
        SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice, messages);
        m_Runtime.OnUpdate();

        logcat.FilterOptions.Filter = "ab";
        Assert.AreEqual(true, filteredResultsWereReused);
        Assert.AreEqual(4, logcat.FilteredEntries.Count);

        logcat.FilterOptions.Filter = "b";
        Assert.AreEqual(false, filteredResultsWereReused);
        Assert.AreEqual(4, logcat.FilteredEntries.Count);

        logcat.FilterOptions.Filter = "abc";
        Assert.AreEqual(true, filteredResultsWereReused);
        Assert.AreEqual(4, logcat.FilteredEntries.Count);

        // Match Case is set to false, we can reuse results filtered with 'a' letter
        logcat.FilterOptions.Filter = "ABCD";
        Assert.AreEqual(true, filteredResultsWereReused);
        Assert.AreEqual(3, logcat.FilteredEntries.Count);

        // Since 'ABCD' filter filters more than 'abc', we cannot reuse previous results
        logcat.FilterOptions.Filter = "abc";
        Assert.AreEqual(false, filteredResultsWereReused);
        Assert.AreEqual(4, logcat.FilteredEntries.Count);

        // When MatchCase changes from false to true, we can reuse previous results
        logcat.FilterOptions.MatchCase = true;
        Assert.AreEqual(true, filteredResultsWereReused);
        Assert.AreEqual(3, logcat.FilteredEntries.Count);

        // When MatchCase changes from true to false, previous results might be not enough
        logcat.FilterOptions.MatchCase = false;
        Assert.AreEqual(false, filteredResultsWereReused);
        Assert.AreEqual(4, logcat.FilteredEntries.Count);

        // Can reuse result, since we're changing filter from abc to abcd
        logcat.FilterOptions.Filter = "abcd";
        Assert.AreEqual(true, filteredResultsWereReused);
        Assert.AreEqual(3, logcat.FilteredEntries.Count);

        // When using regex we're always using raw entries for filtering
        logcat.FilterOptions.UseRegularExpressions = true;
        Assert.AreEqual(false, filteredResultsWereReused);
        Assert.AreEqual(3, logcat.FilteredEntries.Count);

        // Even though results could be reused, it's quite hard to determine with regex if previous results was higher set of current results
        logcat.FilterOptions.Filter = "abcde";
        Assert.AreEqual(false, filteredResultsWereReused);
        Assert.AreEqual(2, logcat.FilteredEntries.Count);

        logcat.Stop();

        ShutdownRuntime();
    }

    [Test]
    public void InvalidRegexMatchesAllMessages()
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

        var logcat = new AndroidLogcat(m_Runtime, null, kDefaultDevice, -1, Priority.Verbose,
            new FilterOptions() { UseRegularExpressions = true },
            new string[] { });
        logcat.Start();
        SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice, messages);

        m_Runtime.OnUpdate();

        logcat.FilterOptions.Filter = "PPP";
        Assert.AreEqual(3, logcat.RawEntries.Count);
        Assert.AreEqual(0, logcat.FilteredEntries.Count);
        Assert.AreEqual(true, logcat.FilterOptions.UseRegularExpressions);

        logcat.FilterOptions.Filter = @"\P";
        Assert.AreEqual(3, logcat.RawEntries.Count);
        Assert.AreEqual(3, logcat.FilteredEntries.Count);
        Assert.AreEqual(true, logcat.FilterOptions.UseRegularExpressions);

        logcat.Stop();

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
        InitRuntime();

        m_Runtime.Settings.MaxCachedMessageCount = 20;
        m_Runtime.Settings.MaxDisplayedMessageCount = 20;
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

        m_Runtime.Settings.MaxDisplayedMessageCount = 10;
        AndroidLogcatUtilities.ApplySettings(m_Runtime, logcat);
        Assert.AreEqual(10, logcat.FilteredEntries.Count);
        Assert.AreEqual(20, logcat.RawEntries.Count);
        Assert.AreEqual(20.ToString(), logcat.FilteredEntries[0].message);

        // Remove limiters and see that no messages are dropped
        m_Runtime.Settings.MaxCachedMessageCount = 0;
        m_Runtime.Settings.MaxDisplayedMessageCount = 0;
        for (int i = 0; i < 30; i++)
            SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice,
                new[] { $"10-25 14:27:56.862  1  2255 I chromium: {i + 30}" });
        m_Runtime.OnUpdate();

        Assert.AreEqual(40, logcat.FilteredEntries.Count);
        Assert.AreEqual(50, logcat.RawEntries.Count);

        logcat.Stop();

        ShutdownRuntime();
    }

    [Test]
    public void CanApplySettingsWithNullLogcat()
    {
        InitRuntime();
        AndroidLogcatUtilities.ApplySettings(m_Runtime, null);
        ShutdownRuntime();
    }

    [Test]
    public void MessageSelectionWorks()
    {
        InitRuntime();

        const int kMaxFilteredMessages = 20;

        m_Runtime.Settings.MaxDisplayedMessageCount = kMaxFilteredMessages;
        var logcat = new AndroidLogcat(m_Runtime, null, kDefaultDevice, -1, Priority.Verbose, new FilterOptions(), new string[] { });
        logcat.Start();

        for (int i = 0; i < kMaxFilteredMessages + 10; i++)
            SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice,
                new[] { $"10-25 14:27:56.862  1  2255 I chromium: {i}" });

        m_Runtime.OnUpdate();

        var entries = logcat.GetSelectedFilteredEntries(out var minIdx, out var maxIdx);
        Assert.AreEqual(0, entries.Count);
        Assert.AreEqual(int.MaxValue, minIdx);
        Assert.AreEqual(int.MinValue, maxIdx);

        logcat.FilteredEntries[2].Selected = true;
        logcat.FilteredEntries[18].Selected = true;

        entries = logcat.GetSelectedFilteredEntries(out minIdx, out maxIdx);
        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual(2, minIdx);
        Assert.AreEqual(18, maxIdx);

        // Entry at index 2 will be stripped
        m_Runtime.Settings.MaxDisplayedMessageCount = 10;
        AndroidLogcatUtilities.ApplySettings(m_Runtime, logcat);

        entries = logcat.GetSelectedFilteredEntries(out minIdx, out maxIdx);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(8, minIdx);
        Assert.AreEqual(8, maxIdx);

        logcat.Stop();

        ShutdownRuntime();
    }

    [Test]
    public void MessageClearSelectionWorks()
    {
        var messages = new[]
        {
            "aaa",
            "bbb",
            "ccc"
        };
        InitRuntime();

        var logcat = new AndroidLogcat(m_Runtime, null, kDefaultDevice, -1, Priority.Verbose, new FilterOptions(), new string[] { });
        logcat.Start();

        foreach (var m in messages)
        {
            SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice,
                new[] { $"10-25 14:27:56.862  1  2255 I chromium: {m}" });
        }

        m_Runtime.OnUpdate();
        // Select all raw entries
        foreach (var e in logcat.RawEntries)
            e.Selected = true;

        logcat.FilterOptions.Filter = "aaa";
        Assert.AreEqual(1, logcat.FilteredEntries.Count);
        Assert.AreEqual(1, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);

        logcat.ClearSelectedEntries();
        Assert.AreEqual(0, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);
        Assert.AreEqual(0, logcat.RawEntries.Where(e => e.Selected).ToArray().Length);

        logcat.FilterOptions.Filter = string.Empty;
        Assert.AreEqual(0, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);
        Assert.AreEqual(0, logcat.RawEntries.Where(e => e.Selected).ToArray().Length);

        logcat.Stop();

        ShutdownRuntime();
    }


    [Test]
    public void MessageSelectAllWorks()
    {
        var messages = new[]
        {
            "aaa",
            "bbb",
            "ccc"
        };
        InitRuntime();

        var logcat = new AndroidLogcat(m_Runtime, null, kDefaultDevice, -1, Priority.Verbose, new FilterOptions(), new string[] { });
        logcat.Start();

        foreach (var m in messages)
        {
            SupplyFakeMessages((AndroidLogcatFakeMessageProvider)logcat.MessageProvider, kDefaultDevice,
                new[] { $"10-25 14:27:56.862  1  2255 I chromium: {m}" });
        }

        m_Runtime.OnUpdate();

        // Select All why filter was set to 'aaa'
        logcat.FilterOptions.Filter = "aaa";
        logcat.SelectAllFilteredEntries();
        Assert.AreEqual(1, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);
        Assert.AreEqual(1, logcat.RawEntries.Where(e => e.Selected).ToArray().Length);

        logcat.FilterOptions.Filter = string.Empty;
        Assert.AreEqual(1, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);
        Assert.AreEqual(1, logcat.RawEntries.Where(e => e.Selected).ToArray().Length);

        // Select All why filter was set to nothing
        logcat.SelectAllFilteredEntries();
        Assert.AreEqual(3, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);
        Assert.AreEqual(3, logcat.RawEntries.Where(e => e.Selected).ToArray().Length);

        // Again select all with filter 'aaa'.
        // Note: it should deselect entries in raw, so when we reset filter they wouldn't be selected
        logcat.FilterOptions.Filter = "aaa";
        logcat.SelectAllFilteredEntries();
        Assert.AreEqual(1, logcat.GetSelectedFilteredEntries(out var _, out var _).Count);
        Assert.AreEqual(1, logcat.RawEntries.Where(e => e.Selected).ToArray().Length);


        logcat.Stop();

        ShutdownRuntime();
    }
}
