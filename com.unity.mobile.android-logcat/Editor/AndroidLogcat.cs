using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using System.Text;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcat
    {
        private AndroidLogcatRuntimeBase m_Runtime;
        private AndroidBridge.ADB adb;

        private readonly IAndroidLogcatDevice m_Device;
        private readonly int m_PackagePid;
        private readonly Priority m_MessagePriority;
        private readonly string[] m_Tags;
        private readonly LogcatFilterOptions m_FilterOptions;
        private FilterOptions m_LastUsedFilterOptions;
        private List<LogcatEntry> m_RawLogEntries = new List<LogcatEntry>();
        private List<LogcatEntry> m_FilteredLogEntries = new List<LogcatEntry>();

        public IAndroidLogcatDevice Device { get { return m_Device; } }

        public int PackagePid { get { return m_PackagePid; } }

        public Priority MessagePriority { get { return m_MessagePriority; } }

        public string[] Tags { get { return m_Tags; } }

        public event Action<IReadOnlyList<LogcatEntry>> FilteredLogEntriesAdded;

        public event Action<IAndroidLogcatDevice> Disconnected;

        public event Action<IAndroidLogcatDevice> Connected;

        private AndroidLogcatMessageProviderBase m_MessageProvider;

        private List<string> m_CachedLogLines = new List<string>();

        public IReadOnlyList<LogcatEntry> RawEntries => m_RawLogEntries;
        public IReadOnlyList<LogcatEntry> FilteredEntries => m_FilteredLogEntries;
        public IReadOnlyList<LogcatEntry> GetSelectedFilteredEntries(out int minIndex, out int maxIndex)
        {
            minIndex = int.MaxValue;
            maxIndex = int.MinValue;

            var selectedEntries = new List<LogcatEntry>(FilteredEntries.Count);
            for (int i = 0; i < FilteredEntries.Count; i++)
            {
                if (!FilteredEntries[i].Selected)
                    continue;

                if (i < minIndex)
                    minIndex = i;
                if (i > maxIndex)
                    maxIndex = i;
                selectedEntries.Add(FilteredEntries[i]);
            }

            return selectedEntries;
        }

        public void ClearSelectedEntries()
        {
            foreach (var e in RawEntries)
                e.Selected = false;
        }

        public void SelectAllFilteredEntries()
        {
            // Note: we're deselecting all raw entries first, to cover this scenario:
            // - Suppose we have 10 entries
            // - Select All
            // - Set filter which would make 5 filtered entries from those 10
            // - Select All
            // - Clear filter
            // - 10 entries are now visible, but selected are only 5, not 10
            ClearSelectedEntries();

            foreach (var e in FilteredEntries)
                e.Selected = true;
        }

        public FilterOptions FilterOptions => m_FilterOptions;

        public bool IsConnected
        {
            get
            {
                if (m_MessageProvider == null)
                    return false;
                try
                {
                    if (m_MessageProvider.HasExited)
                        return false;

                    if (m_Device == null)
                        return false;

                    return m_Device.State == IAndroidLogcatDevice.DeviceState.Connected;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError(ex.Message);
                    return false;
                }
            }
        }

        public AndroidLogcatMessageProviderBase MessageProvider
        {
            get { return m_MessageProvider; }
        }

        public AndroidLogcat(AndroidLogcatRuntimeBase runtime,
            AndroidBridge.ADB adb,
            IAndroidLogcatDevice device,
            int packagePid,
            Priority priority,
            FilterOptions filterOptions,
            string[] tags)
        {
            this.m_Runtime = runtime;
            this.adb = adb;
            this.m_Device = device;
            this.m_PackagePid = packagePid;
            this.m_MessagePriority = priority;
            this.m_FilterOptions = new LogcatFilterOptions(filterOptions);
            this.m_LastUsedFilterOptions = new FilterOptions(m_FilterOptions);
            this.m_Tags = tags;

            m_FilterOptions.OnFilterChanged = OnFilterChanged;

            LogcatEntry.SetTimeFormat(m_Device.SupportYearFormat ? LogcatEntry.kTimeFormatWithYear : LogcatEntry.kTimeFormatWithoutYear);
        }

        private void ClearEntries()
        {
            m_RawLogEntries.Clear();
            m_FilteredLogEntries.Clear();
        }

        internal bool CanReuseFilteredResults()
        {
            if (m_LastUsedFilterOptions.UseRegularExpressions ||
                m_FilterOptions.UseRegularExpressions)
                return false;

            // When changing match case from true to false, the previous set might not enough for new results
            // But previous set will might be enough when changing Match Case from false to true
            if (m_LastUsedFilterOptions.MatchCase != m_FilterOptions.MatchCase &&
                m_LastUsedFilterOptions.MatchCase &&
                !m_FilterOptions.MatchCase)
                return false;

            return m_FilterOptions.Filter.IndexOf(m_LastUsedFilterOptions.Filter, StringComparison.InvariantCultureIgnoreCase) != -1;
        }

        private void OnFilterChanged()
        {
            // Optimization, reuse previous results if possible
            if (CanReuseFilteredResults())
            {
                FilterEntriesUsingFilteredEntries(m_FilteredLogEntries);
            }
            else
            {
                m_FilteredLogEntries.Clear();
                FilterEntriesUsingRawEntries(m_RawLogEntries);
            }

            m_LastUsedFilterOptions.Filter = m_FilterOptions.Filter;
            m_LastUsedFilterOptions.UseRegularExpressions = m_FilterOptions.UseRegularExpressions;
            m_LastUsedFilterOptions.MatchCase = m_FilterOptions.MatchCase;
        }

        internal void Start()
        {
            // For logcat arguments and more details check https://developer.android.com/studio/command-line/logcat
            m_Runtime.Update += OnUpdate;
            m_MessageProvider = m_Runtime.CreateMessageProvider(adb, MessagePriority, m_Device.SupportsFilteringByPid ? PackagePid : 0, LogPrintFormat, m_Device, OnDataReceived);
            m_MessageProvider.Start();

            Connected?.Invoke(Device);
        }

        internal void Stop()
        {
            m_CachedLogLines.Clear();
            if (m_Runtime != null)
                m_Runtime.Update -= OnUpdate;
            if (m_MessageProvider != null && !m_MessageProvider.HasExited)
            {
                // NOTE: DONT CALL CLOSE, or ADB process will stay alive all the time
                m_MessageProvider.Kill();
            }

            m_MessageProvider = null;
        }

        internal void Clear()
        {
            if (m_MessageProvider != null)
                throw new InvalidOperationException("Cannot clear logcat when logcat process is alive.");

            if (m_Device.State == IAndroidLogcatDevice.DeviceState.Connected)
            {
                // If device is disconnected, this command would freeze, in the console I see message '-Waiting for device-'
                AndroidLogcatInternalLog.Log("{0} -s {1} logcat -c", adb.GetADBPath(), Device.Id);
                var adbOutput = adb.Run(new[] { "-s", Device.Id, "logcat", "-c" }, "Failed to clear logcat.");
                AndroidLogcatInternalLog.Log(adbOutput);
            }
            else
            {
                AndroidLogcatInternalLog.Log($"Device {Device.Id} is not connected (State: {m_Device.State}), cannot clear messages");
            }

            ClearEntries();
        }

        void OnUpdate()
        {
            if (m_MessageProvider == null)
                return;

            if (m_MessageProvider.HasExited)
            {
                Stop();

                Disconnected?.Invoke(Device);

                return;
            }

            ProcessCachedLogLines();
        }

        void ProcessCachedLogLines()
        {
            List<LogcatEntry> entries = new List<LogcatEntry>();
            lock (m_CachedLogLines)
            {
                if (m_CachedLogLines.Count == 0)
                    return;

                var needFilterByPid = !m_Device.SupportsFilteringByPid && PackagePid > 0;
                var needFilterByTags = Tags != null && Tags.Length > 0;
                Regex regex = LogParseRegex;
                foreach (var logLine in m_CachedLogLines)
                {
                    var m = regex.Match(logLine);
                    if (!m.Success)
                    {
                        // The reason we need to check `needFilterByTags` is we don't really want to show the error logs that we can't parse if a tag is chosen.
                        // For logs we can't parse, please refer to https://gitlab.cds.internal.unity3d.com/upm-packages/mobile/mobile-android-logcat/issues/44
                        // And we should remove this check once #44 is fixed completely.
                        if (!needFilterByTags)
                            entries.Add(LogEntryParserErrorFor(logLine));
                        continue;
                    }

                    if (needFilterByPid && Int32.Parse(m.Groups["pid"].Value) != PackagePid)
                        continue;

                    if (needFilterByTags && !MatchTagsFilter(m.Groups["tag"].Value))
                        continue;

                    entries.Add(ParseLogEntry(m));
                }
                m_CachedLogLines.Clear();
            }

            if (entries.Count == 0)
                return;

            m_RawLogEntries.AddRange(entries);

            StripRawEntriesIfNeeded();

            FilterEntriesUsingRawEntries(entries);
        }

        public void StripRawEntriesIfNeeded()
        {
            var rawMaxCount = m_Runtime.Settings.MaxCachedMessageCount;
            if (rawMaxCount > 0 && m_RawLogEntries.Count > rawMaxCount)
                m_RawLogEntries.RemoveRange(0, m_RawLogEntries.Count - rawMaxCount);
        }

        public void StripFilteredEntriesIfNeeded()
        {
            var filteredMaxCount = m_Runtime.Settings.MaxDisplayedMessageCount;
            if (filteredMaxCount > 0 && m_FilteredLogEntries.Count > filteredMaxCount)
                m_FilteredLogEntries.RemoveRange(0, m_FilteredLogEntries.Count - filteredMaxCount);
        }

        private List<LogcatEntry> FilterEntries(IReadOnlyList<LogcatEntry> unfilteredEntries)
        {
            // Set capacity 10% for filtered entries from unfiltered entries to minimize unneeded allocations
            var filteredEntries = new List<LogcatEntry>(unfilteredEntries.Count / 10);
            foreach (var entry in unfilteredEntries)
            {
                if (!m_FilterOptions.Matches(entry.message))
                    continue;
                filteredEntries.Add(entry);
            }

            return filteredEntries;
        }

        private void FilterEntriesUsingRawEntries(IReadOnlyList<LogcatEntry> unfilteredEntries)
        {
            IReadOnlyList<LogcatEntry> filteredEntries;
            if (string.IsNullOrEmpty(m_FilterOptions.Filter))
            {
                filteredEntries = unfilteredEntries.ToList();
            }
            else
            {
                filteredEntries = FilterEntries(unfilteredEntries);
            }

            if (filteredEntries.Count == 0)
                return;

            m_FilteredLogEntries.AddRange(filteredEntries);
            FilteredLogEntriesAdded?.Invoke(filteredEntries);

            StripFilteredEntriesIfNeeded();
        }

        private void FilterEntriesUsingFilteredEntries(IReadOnlyList<LogcatEntry> unfilteredEntries)
        {
            if (string.IsNullOrEmpty(m_FilterOptions.Filter))
                return;

            var filteredEntries = FilterEntries(unfilteredEntries);
            m_FilteredLogEntries = filteredEntries;
            if (filteredEntries.Count == 0)
                return;
            FilteredLogEntriesAdded?.Invoke(filteredEntries);

            // No need to strip, since filtering from filtered entries, can only shrink the list, but not grow
        }

        private LogcatEntry LogEntryParserErrorFor(string msg)
        {
            return new LogcatEntry(msg);
        }

        private bool MatchTagsFilter(string tagInMsg)
        {
            foreach (var tag in Tags)
            {
                if (tagInMsg.Contains(tag))
                    return true;
            }

            return false;
        }

        private LogcatEntry ParseLogEntry(Match m)
        {
            DateTime dateTime;
            var dateValue = m.Groups["date"].Value;
            if (LogPrintFormat == kThreadTime)
                dateValue = "1999-" + dateValue;

            try
            {
                dateTime = DateTime.Parse(dateValue);
            }
            catch (Exception ex)
            {
                dateTime = new DateTime();
                AndroidLogcatInternalLog.Log("Failed to parse date: " + dateValue + "\n" + ex.Message);
            }

            var entry = new LogcatEntry(
                dateTime,
                Int32.Parse(m.Groups["pid"].Value),
                Int32.Parse(m.Groups["tid"].Value),
                PriorityStringToEnum(m.Groups["priority"].Value),
                m.Groups["tag"].Value,
                m.Groups["msg"].Value);

            return entry;
        }

        private Priority PriorityStringToEnum(string priority)
        {
            switch (priority)
            {
                case "V": return Priority.Verbose;
                case "D": return Priority.Debug;
                case "I": return Priority.Info;
                case "W": return Priority.Warn;
                case "E": return Priority.Error;
                case "F": return Priority.Fatal;

                default:
                    throw new InvalidOperationException(string.Format("Invalid `priority` ({0}) in log entry.", priority));
            }
        }

        private void OnDataReceived(string message)
        {
            // You can receive null string, when you put out USB cable out of PC and logcat connection is lost
            // You can receive empty string on old devices like LG 5.0
            // Note: Even if the logcat message is empty, the incoming string still have to contain info about time/pid/tid/etc
            //       If it contains nothing, ignore it
            if (string.IsNullOrEmpty(message))
                return;

            lock (m_CachedLogLines)
            {
                m_CachedLogLines.Add(message);
            }
        }

        private static int s_DebuggingMessageId;

        private void DbgAddLogLines(int count)
        {
            var entries = new List<LogcatEntry>(count);
            for (int i = 0; i < count; i++)
            {
                var pid = 123;
                var tid = 234;
                OnDataReceived($"2022-01-31 12:43:40.003   {pid}   {tid} I DummyTag: Dummy Message {s_DebuggingMessageId}");
                s_DebuggingMessageId++;
            }

            ProcessCachedLogLines();
        }
        internal void DoDebuggingGUI()
        {
            if (GUILayout.Button("Add Log line", AndroidLogcatStyles.toolbarButton))
            {
                DbgAddLogLines(1);
            }
            if (GUILayout.Button("Add Log lines", AndroidLogcatStyles.toolbarButton))
            {
                DbgAddLogLines(10000);
            }
            GUILayout.Label($"Raw: {m_RawLogEntries.Count} Filtered: {m_FilteredLogEntries.Count}");
        }

        internal Regex LogParseRegex
        {
            get { return m_Device.SupportYearFormat ? m_LogCatEntryYearRegex : m_LogCatEntryThreadTimeRegex; }
        }

        /// <summary>
        /// Returns log print format used with adb logcat -v LogPrintFormat
        /// Note: Old android devices don't support all -v formats
        /// For ex., on Android 5.1.1 only these -v are available [brief process tag thread raw time threadtime long]
        /// While on Android 7.0, -v can have [brief color epoch long monotonic printable process raw tag thread threadtime time uid usec UTC year zone]
        /// </summary>
        internal string LogPrintFormat
        {
            get { return m_Device.SupportYearFormat ? kYearTime : kThreadTime; }
        }

        internal static Regex m_CrashMessageRegex = new Regex(@"^\s*#\d{2}\s*pc\s([a-fA-F0-9]{8}).*(libunity\.so|libmain\.so)", RegexOptions.Compiled);
        // Regex for messages produced via 'adb logcat -s -v year *:V'
        internal static Regex m_LogCatEntryYearRegex = new Regex(@"(?<date>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3})\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<priority>[VDIWEFS])\s+(?<tag>.+?)\s*:\s(?<msg>.*)", RegexOptions.Compiled);

        // Regex for messages produced via 'adb logcat -s -v threadtime *:V'
        internal static Regex m_LogCatEntryThreadTimeRegex = new Regex(@"(?<date>\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3})\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<priority>[VDIWEFS])\s+(?<tag>.+?)\s*:\s(?<msg>.*)", RegexOptions.Compiled);


        internal static readonly int kUnityHashCode = "Unity".GetHashCode();
        internal static readonly int kCrashHashCode = "CRASH".GetHashCode();
        internal static readonly int kDebugHashCode = "DEBUG".GetHashCode();

        // Log PrintFormats
        internal const string kThreadTime = "threadtime";
        internal const string kYearTime = "year";
    }
}
