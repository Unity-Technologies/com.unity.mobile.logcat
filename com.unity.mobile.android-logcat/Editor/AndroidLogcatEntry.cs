using System;

namespace Unity.Android.Logcat
{
    class LogcatEntry
    {
        [Flags]
        enum Flags
        {
            None,
            Selected
        }

        public const string kTimeFormatWithYear = "yyyy/MM/dd HH:mm:ss.fff";
        public const string kTimeFormatWithoutYear = "MM/dd HH:mm:ss.fff";
        public static string s_TimeFormat = kTimeFormatWithYear;
        private Flags m_Flags;

        public bool Selected
        {
            set
            {
                if (value)
                    m_Flags |= Flags.Selected;
                else
                    m_Flags &= ~Flags.Selected;
            }

            get
            {
                return m_Flags.HasFlag(Flags.Selected);
            }
        }

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
