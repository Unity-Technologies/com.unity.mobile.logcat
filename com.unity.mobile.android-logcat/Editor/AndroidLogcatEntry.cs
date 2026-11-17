using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Serializable for a recording, which saves the messages logged while it ran. A
    /// recording's entries are in UTC, read with '-v epoch'.
    /// </summary>
    [NoAutoStaticsCleanup]
    [Serializable]
    class LogcatEntry : ISerializationCallbackReceiver
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

        // dateTime as saved, which JsonUtility cannot write: microseconds since the epoch.
        [SerializeField]
        long timeUs;

        // For JsonUtility.
        LogcatEntry()
        {
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

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            timeUs = (dateTime.Ticks - DateTime.UnixEpoch.Ticks) / 10;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            dateTime = DateTime.SpecifyKind(DateTime.UnixEpoch.AddTicks(timeUs * 10), DateTimeKind.Utc);
        }

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
