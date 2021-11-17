using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using System.Text;

namespace Unity.Android.Logcat
{

    class LogcatEntry
    {
        public const string kTimeFormatWithYear = "yyyy/MM/dd HH:mm:ss.fff";
        public const string kTimeFormatWithoutYear = "MM/dd HH:mm:ss.fff";
        public static string s_TimeFormat = kTimeFormatWithYear;
        public LogcatEntry(string msg)
        {
            message = msg;
            tag = string.Empty;
            dateTime = new DateTime();
            processId = -1;
            threadId = -1;
            priority = Priority.Info;
            this.message = this.message.TrimEnd(new[] { '\r', '\n' });
        }

        public LogcatEntry(LogcatEntry entry)
        {
            this.dateTime = entry.dateTime;
            this.processId = entry.processId;
            this.threadId = entry.threadId;
            this.priority = entry.priority;
            this.tag = entry.tag;
            this.message = entry.message;
        }

        public LogcatEntry(DateTime dateTime, int processId, int threadId, Priority priority, string tag, string message)
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
}
