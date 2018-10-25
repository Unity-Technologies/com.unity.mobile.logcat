using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;

[assembly: InternalsVisibleTo("Unity.Mobile.AndroidLogcat.EditorTests")]

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
            public const string kTimeFormat = "yyyy/MM/dd HH:mm:ss.fff";
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

            public override string ToString() => $"{dateTime.ToString(kTimeFormat)} {processId} {threadId} {priority} {tag}: {message}";
        }

        protected struct BuildInfo
        {
            public string buildType;
            public string scriptingImplementation;
            public string cpu;
        }

        private ADB adb;

        public AndroidDevice Device { get; }

        public int PackagePID { get; }

        public Priority MessagePriority { get; }

        public string Filter { get; }

        public string[] Tags { get; }

        public event Action<List<LogEntry>> LogEntriesAdded;

        public event Action<string> DeviceDisconnected;

        public event Action<string> DeviceConnected;

        private Process m_LogcatProcess;

        private List<string> m_CachedLogLines = new List<string>();

        private string m_LastLogcatCommand = "";

        public bool IsConnected
        {
            get
            {
                if (m_LogcatProcess == null)
                    return false;
                try
                {
                    return !m_LogcatProcess.HasExited;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError(ex.Message);
                    return false;
                }
            }
        }

        public AndroidLogcat(ADB adb, AndroidDevice device, int packagePID, Priority priority, string filter, bool filterIsRegex, string[] tags)
        {
            this.adb = adb;
            this.Device = device;
            this.PackagePID = packagePID;
            this.MessagePriority = priority;
            this.Filter =  filterIsRegex  ? filter : Regex.Escape(filter);
            this.Tags = tags;
        }

        internal void Start()
        {
            // For logcat arguments and more details check https://developer.android.com/studio/command-line/logcat
            EditorApplication.update += OnUpdate;

            m_LastLogcatCommand = LogcatArguments();
            m_LogcatProcess = StartADB(m_LastLogcatCommand);

            m_LogcatProcess.BeginOutputReadLine();
            m_LogcatProcess.BeginErrorReadLine();

            DeviceConnected?.Invoke(Device.Id);
        }

        private string LogcatArguments()
        {
            var filterArg = string.Empty;
            if (!string.IsNullOrEmpty(Filter))
            {
                filterArg = $@" --regex ""{Filter}""";
            }

            var p = PriorityEnumToString(MessagePriority);
            var tagLine = Tags.Length > 0 ? string.Join(" ", Tags.Select(m => m + ":" + p + " ").ToArray()) : $"*:{p} ";
            if (PackagePID <= 0)
                return $"-s {Device.Id} logcat -s -v {LogPrintFormat} {tagLine}{filterArg}";

            return $"-s {Device.Id} logcat --pid={PackagePID} -s -v {LogPrintFormat} {tagLine}{filterArg}";
        }

        internal void Stop()
        {
            m_CachedLogLines.Clear();
            m_BuildInfos.Clear();
            EditorApplication.update -= OnUpdate;
            if (m_LogcatProcess != null && !m_LogcatProcess.HasExited)
            {
                AndroidLogcatInternalLog.Log($"Stopping logcat (process id {m_LogcatProcess.Id})");
                // NOTE: DONT CALL CLOSE, or ADB process will stay alive all the time
                m_LogcatProcess.Kill();
            }

            m_LogcatProcess = null;
        }

        internal void Clear()
        {
            if (m_LogcatProcess != null)
                throw new InvalidOperationException("Cannot clear logcat when logcat process is alive.");

            AndroidLogcatInternalLog.Log($"{adb.GetADBPath()} -s {Device.Id} logcat -c");
            var adbOutput = adb.Run(new[] { "-s", Device.Id, "logcat", "-c" }, "Failed to clear logcat.");
            AndroidLogcatInternalLog.Log(adbOutput);
        }

        private Process StartADB(string arguments)
        {
            AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), arguments);
            Process logcatProcess = new Process();
            logcatProcess.StartInfo.FileName = adb.GetADBPath();
            logcatProcess.StartInfo.Arguments = arguments;
            logcatProcess.StartInfo.RedirectStandardError = true;
            logcatProcess.StartInfo.RedirectStandardOutput = true;
            logcatProcess.StartInfo.UseShellExecute = false;
            logcatProcess.StartInfo.CreateNoWindow = true;
            logcatProcess.OutputDataReceived += OutputDataReceived;
            logcatProcess.ErrorDataReceived += ErrorDataReceived;
            logcatProcess.Start();

            return logcatProcess;
        }

        void OnUpdate()
        {
            if (m_LogcatProcess == null)
                return;

            if (m_LogcatProcess.HasExited)
            {
                Stop();
                DeviceDisconnected?.Invoke(Device.Id);

                return;
            }

            List<LogEntry> entries = null;
            lock (m_CachedLogLines)
            {
                if (m_CachedLogLines.Count == 0)
                    return;
                entries = m_CachedLogLines.Select(e =>
                {
                    var m = m_LogCatEntryThreadTimeRegex.Match(e);
                    return m.Success ? ParseLogEntry(m) : LogEntryParserErrorFor(e);
                }).ToList();
                m_CachedLogLines.Clear();
            }

            if (entries == null)
                return;

            ResolveStackTrace(entries);
            LogEntriesAdded(entries);
        }

        private LogEntry LogEntryParserErrorFor(string msg)
        {
            return new LogEntry(msg);
        }

        private LogEntry ParseLogEntry(Match m)
        {
            DateTime dateTime;
            switch (LogPrintFormat)
            {
                case kThreadTime:
                    dateTime = DateTime.Parse("1999-" + m.Groups["date"].Value);
                    break;
                case kYearTime:
                    dateTime = DateTime.Parse(m.Groups["date"].Value);
                    break;
                default:
                    throw new NotImplementedException("Please implement date parsing for log format: " + LogPrintFormat);
            }   
            var entry = new LogEntry(
                dateTime,
                Int32.Parse(m.Groups["pid"].Value),
                Int32.Parse(m.Groups["tid"].Value),
                PriorityStringToEnum(m.Groups["priority"].Value),
                m.Groups["tag"].Value,
                m.Groups["msg"].Value);

            if ((entry.priority == Priority.Info && entry.tag.GetHashCode() == kUnityHashCode && entry.message.StartsWith("Built from")) ||
                (entry.priority == Priority.Error && entry.tag.GetHashCode() == kCrashHashCode && entry.message.StartsWith("Build type")))
            {
                m_BuildInfos[entry.processId] = ParseBuildInfo(entry.message);
            }

            if (entry.priority == Priority.Fatal && entry.tag.GetHashCode() == kDebugHashCode && entry.message.StartsWith("pid:"))
            {
                // Crash reported by Android for some pid, need to update buildInfo information for this new pid as well
                ParseCrashBuildInfo(entry.processId, entry.message);
            }

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
                    throw new InvalidOperationException($"Invalid `priority` ({priority}) in log entry.");
            }
        }

        private BuildInfo ParseBuildInfo(string msg)
        {
            BuildInfo buildInfo;

            var reg = new Regex(@"Build type '(.+)',\s+Scripting Backend '(.+)',\s+CPU '(.+)'");
            Match match = reg.Match(msg);

            buildInfo.buildType = match.Groups[1].Value.ToLower();
            buildInfo.scriptingImplementation = match.Groups[2].Value.ToLower();
            buildInfo.cpu = match.Groups[3].Value.ToLower();
            return buildInfo;
        }

        private void ParseCrashBuildInfo(int processId, string msg)
        {
            var reg = new Regex(@"pid: '(.+)'");
            Match match = reg.Match(msg);

            if (match.Success)
            {
                int pid = Int32.Parse(match.Groups[1].Value);
                if (pid != processId && m_BuildInfos.ContainsKey(pid))
                    m_BuildInfos[processId] = m_BuildInfos[pid];
            }
        }

        public struct UnresolvedAddress
        {
            public int logEntryIndex;
            public string unresolvedAddress;
        };

        private void ResolveStackTrace(List<LogEntry> entries)
        {
            var unresolvedAddresses = new Dictionary<KeyValuePair<BuildInfo, string>, List<UnresolvedAddress>>();

            // Gather unresolved address if there are any
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                // Only process stacktraces from Error/Fatal priorities
                if (entry.priority != Priority.Error && entry.priority != Priority.Fatal)
                    continue;

                // Only process stacktraces if tag is "CRASH" or "DEBUG"
                if (entry.tag.GetHashCode() != kCrashHashCode && entry.tag.GetHashCode() != kDebugHashCode)
                    continue;

                BuildInfo buildInfo;
                // Unknown build info, that means we don't know where the symbols are located
                if (!m_BuildInfos.TryGetValue(entry.processId, out buildInfo))
                    continue;

                string address, libName;
                if (!ParseCrashMessage(entry.message, out address, out libName))
                    continue;

                List<UnresolvedAddress> addresses;
                var key = new KeyValuePair<BuildInfo, string>(buildInfo, libName);
                if (!unresolvedAddresses.TryGetValue(key, out addresses))
                    unresolvedAddresses[key] = new List<UnresolvedAddress>();

                unresolvedAddresses[key].Add(new UnresolvedAddress() { logEntryIndex = i, unresolvedAddress = address });
            }

            var engineDirectory = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);


            // Resolve addresses
            foreach (var u in unresolvedAddresses)
            {
                var buildInfo = u.Key.Key;
                var libName = u.Key.Value;

                var addresses = u.Value;
                string[] paths = { engineDirectory, "Variations", buildInfo.scriptingImplementation, buildInfo.buildType, "Symbols", buildInfo.cpu, libName };
                var libpath = CombinePaths(paths);

                // For optimizations purposes, we batch addresses which belong to same library, so addr2line can be ran less
                try
                {
                    var result = Addr2LineWrapper.Run(libpath, addresses.Select(m => m.unresolvedAddress));
                    for (int i = 0; i < addresses.Count; i++)
                    {
                        var idx = addresses[i].logEntryIndex;
                        var append = string.IsNullOrEmpty(result[i]) ? "(Not Resolved)" : result[i];
                        entries[idx] = new LogEntry(entries[idx]) { message = ModifyLogEntry(entries[idx].message, append, false) };
                    }
                }
                catch (Exception ex)
                {
                    for (int i = 0; i < addresses.Count; i++)
                    {
                        var idx = addresses[i].logEntryIndex;
                        entries[idx] = new LogEntry(entries[idx]) { message = ModifyLogEntry(entries[idx].message, "(Addr2Line failure)", true) };
                        var errorMessage = new StringBuilder();
                        errorMessage.AppendLine("Addr2Line failure");
                        errorMessage.AppendLine("Scripting Backend: " + buildInfo.scriptingImplementation);
                        errorMessage.AppendLine("Build Type: " + buildInfo.buildType);
                        errorMessage.AppendLine("CPU: " + buildInfo.cpu);
                        errorMessage.AppendLine(ex.Message);
                        UnityEngine.Debug.LogError(errorMessage.ToString());
                    }
                }
            }
        }

        private string CombinePaths(string[] paths)
        {
            // Unity hasn't implemented System.IO.Path(string[]), we have to do it on our own.
            if (paths.Length == 0)
                return "";

            string path = paths[0];
            for (int i = 1; i < paths.Length; ++i)
                path = System.IO.Path.Combine(path, paths[i]);
            return path;
        }

        private bool ParseCrashMessage(string msg, out string address, out string libName)
        {
            var match = m_CrashMessageRegex.Match(msg);
            if (match.Success)
            {
                address = match.Groups[1].Value;
                libName = match.Groups[2].Value;
                return true;
            }
            address = null;
            libName = null;
            return false;
        }

        private string ModifyLogEntry(string msg, string appendText, bool keeplOriginalMessage)
        {
            if (keeplOriginalMessage)
            {
                return msg + " " + appendText;
            }
            else
            {
                var match = m_CrashMessageRegex.Match(msg);
                return match.Success ? match.Groups[0].Value + " " + appendText : msg + " " + appendText;
            }
        }

        private string PriorityEnumToString(Priority priority)
        {
            return priority.ToString().Substring(0, 1);
        }

        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            lock (m_CachedLogLines)
            {
                m_CachedLogLines.Add(e.Data);
            }
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            // You can receive null string, when you put out USB cable out of PC and logcat connection is lost
            if (string.IsNullOrEmpty(e.Data))
                return;

            lock (m_CachedLogLines)
            {
                m_CachedLogLines.Add(e.Data);
            }
        }

        /// <summary>
        /// Returns log print format used with adb logcat -v LogPrintFormat
        /// Note: Old android devices don't support all -v formats 
        /// For ex., on Android 5.1.1 only these -v are available [brief process tag thread raw time threadtime long]
        /// While on Android 7.0, -v can have [brief color epoch long monotonic printable process raw tag thread threadtime time uid usec UTC year zone]
        /// </summary>
        internal string LogPrintFormat
        {
            get { return kThreadTime; }
        }

        private Dictionary<int, BuildInfo> m_BuildInfos = new Dictionary<int, BuildInfo>();

        internal static Regex m_CrashMessageRegex = new Regex(@"^\s*#\d{2}\s*pc\s([a-fA-F0-9]{8}).*(libunity\.so|libmain\.so)", RegexOptions.Compiled);
        // Regex for messages produced via 'adb logcat -s -v year *:V'
        internal static Regex m_LogCatEntryYearRegex = new Regex(@"(?<date>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3})\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<priority>[VDIWEFS])\s+(?<tag>[^:\s]+)?\s*:\s(?<msg>.*)", RegexOptions.Compiled);

        // Regex for messages produced via 'adb logcat -s -v threadtime *:V'
        internal static Regex m_LogCatEntryThreadTimeRegex = new Regex(@"(?<date>\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3})\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<priority>[VDIWEFS])\s+(?<tag>[^:\s]+)?\s*:\s(?<msg>.*)", RegexOptions.Compiled);


        internal static readonly int kUnityHashCode = "Unity".GetHashCode();
        internal static readonly int kCrashHashCode = "CRASH".GetHashCode();
        internal static readonly int kDebugHashCode = "DEBUG".GetHashCode();

        // Log PrintFormats
        internal const string kThreadTime = "threadtime";
        internal const string kYearTime = "year";
    }
}