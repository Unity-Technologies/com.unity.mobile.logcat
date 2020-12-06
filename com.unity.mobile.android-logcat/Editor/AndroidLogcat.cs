using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using System.Text;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcat
    {
        public enum Priority
        {
            Verbose,
            Debug,
            Info,
            Warn,
            Error,
            Fatal
        }

        public struct LogEntry
        {
            public const string kTimeFormatWithYear = "yyyy/MM/dd HH:mm:ss.fff";
            public const string kTimeFormatWithoutYear = "MM/dd HH:mm:ss.fff";
            public static string s_TimeFormat = kTimeFormatWithYear;
            public LogEntry(string msg)
            {
                message =  msg;
                tag = string.Empty;
                dateTime = new DateTime();
                processId = -1;
                threadId = -1;
                priority = Priority.Info;
                this.message = this.message.TrimEnd(new[] { '\r', '\n' });
            }

            public LogEntry(LogEntry entry)
            {
                this.dateTime = entry.dateTime;
                this.processId = entry.processId;
                this.threadId = entry.threadId;
                this.priority = entry.priority;
                this.tag = entry.tag;
                this.message = entry.message;
            }

            public LogEntry(DateTime dateTime, int processId, int threadId, Priority priority, string tag, string message)
            {
                this.dateTime = dateTime;
                this.processId = processId;
                this.threadId = threadId;
                this.priority = priority;
                this.tag = tag ?? string.Empty;
                this.message = message ?? string.Empty;
                this.message = this.message.TrimEnd(new[] { '\r', '\n' });
            }

            public DateTime dateTime;
            public int processId;
            public int threadId;
            public Priority priority;
            public string tag;
            public string message;

            public override string ToString()
            {
                return string.Format("{0} {1} {2} {3} {4}: {5}", dateTime.ToString(s_TimeFormat), processId, threadId, priority, tag, message);
            }

            public static void SetTimeFormat(string timeFormat)
            {
                s_TimeFormat = timeFormat;
            }
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private AndroidBridge.ADB adb;

        private readonly IAndroidLogcatDevice m_Device;
        private readonly int m_PackagePid;
        private readonly Priority m_MessagePriority;
        private string m_Filter;
        private bool m_FilterIsRegex;
        private Regex m_ManualFilterRegex;
        private readonly string[] m_Tags;

        public IAndroidLogcatDevice Device { get { return m_Device; } }

        public int PackagePid { get { return m_PackagePid; } }

        public Priority MessagePriority { get { return m_MessagePriority; } }

        public string Filter { get { return m_Filter; }  set { m_Filter = value; } }

        public string[] Tags { get { return m_Tags; } }

        public event Action<List<LogEntry>> LogEntriesAdded;

        public event Action<IAndroidLogcatDevice> Disconnected;

        public event Action<IAndroidLogcatDevice> Connected;

        private AndroidLogcatMessageProviderBase m_MessageProvider;

        private List<string> m_CachedLogLines = new List<string>();

        public bool IsConnected
        {
            get
            {
                if (m_MessageProvider == null)
                    return false;
                try
                {
                    return !m_MessageProvider.HasExited;
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

        public AndroidLogcat(AndroidLogcatRuntimeBase runtime, AndroidBridge.ADB adb, IAndroidLogcatDevice device, int packagePid, Priority priority, string filter, bool filterIsRegex, string[] tags)
        {
            this.m_Runtime = runtime;
            this.adb = adb;
            this.m_Device = device;
            this.m_PackagePid = packagePid;
            this.m_MessagePriority = priority;
            this.m_FilterIsRegex = filterIsRegex;
            InitFilterRegex(filter);
            this.m_Tags = tags;

            LogEntry.SetTimeFormat(m_Device.SupportYearFormat ? LogEntry.kTimeFormatWithYear : LogEntry.kTimeFormatWithoutYear);
        }

        private void InitFilterRegex(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return;

            if (m_Device.SupportsFilteringByRegex)
            {
                // When doing searching by filter, we use --regex command.
                // Note: there's no command line argument to disable or enable regular expressions for logcat
                // Thus when we want to disable regular expressions, we simply provide filter with escaped characters to --regex command
                this.m_Filter = m_FilterIsRegex ? filter : Regex.Escape(filter);
                return;
            }

            this.m_Filter = filter;
            if (!this.m_FilterIsRegex)
                return;

            try
            {
                m_ManualFilterRegex = new Regex(m_Filter, RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Input search filter '{m_Filter}' is not a valid regular expression.\n{ex}");

                // Silently disable filtering if supplied regex is wrong
                m_Filter = string.Empty;
                m_ManualFilterRegex = null;
                m_FilterIsRegex = false;
            }
        }

        internal void Start()
        {
            // For logcat arguments and more details check https://developer.android.com/studio/command-line/logcat
            m_Runtime.Update += OnUpdate;
            m_MessageProvider = m_Runtime.CreateMessageProvider(adb, m_Device.SupportsFilteringByRegex ? Filter : string.Empty, MessagePriority, m_Device.SupportsFilteringByPid ? PackagePid : 0, LogPrintFormat, m_Device, OnDataReceived);
            m_MessageProvider.Start();

            Connected?.Invoke(Device);
        }

        internal void Stop()
        {
            m_CachedLogLines.Clear();
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

            AndroidLogcatInternalLog.Log("{0} -s {1} logcat -c", adb.GetADBPath(), Device.Id);
            var adbOutput = adb.Run(new[] { "-s", Device.Id, "logcat", "-c" }, "Failed to clear logcat.");
            AndroidLogcatInternalLog.Log(adbOutput);
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

            List<LogEntry> entries = new List<LogEntry>();
            lock (m_CachedLogLines)
            {
                if (m_CachedLogLines.Count == 0)
                    return;

                var needFilterByPid = !m_Device.SupportsFilteringByPid && PackagePid > 0;
                var needFilterByTags = Tags != null && Tags.Length > 0;
                var needFilterBySearch = !m_Device.SupportsFilteringByRegex  && !string.IsNullOrEmpty(Filter);
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

                    if (needFilterBySearch && !MatchSearchFilter(m.Groups["msg"].Value))
                        continue;

                    entries.Add(ParseLogEntry(m));
                }
                m_CachedLogLines.Clear();
            }

            if (entries.Count == 0)
                return;

            LogEntriesAdded(entries);
        }

        private LogEntry LogEntryParserErrorFor(string msg)
        {
            return new LogEntry(msg);
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

        private bool MatchSearchFilter(string msg)
        {
            return m_FilterIsRegex ? m_ManualFilterRegex.Match(msg).Success : msg.Contains(Filter);
        }

        private LogEntry ParseLogEntry(Match m)
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

            var entry = new LogEntry(
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

        internal Regex LogParseRegex
        {
            get { return m_Device.SupportYearFormat  ? m_LogCatEntryYearRegex : m_LogCatEntryThreadTimeRegex; }
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
