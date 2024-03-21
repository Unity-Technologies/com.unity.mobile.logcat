using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatInternalLog : EditorWindow
    {
        class InternalLogEntry
        {
            public bool Selected { get; set; }
            public string Entry { get; set; }
        }

        const int kMaxInternalLogMessages = 10000;
        static AndroidLogcatInternalLog ms_Instance = null;
        static List<InternalLogEntry> ms_LogEntries = new List<InternalLogEntry>();

        Vector2 m_ScrollPosition = Vector2.zero;
        Rect m_ScrollArea;
        float m_MaxEntryWidth;
        static bool s_RecalculateMaxEntryWidth;

        public static void ShowLog(bool immediate)
        {
            if (ms_Instance == null)
                ms_Instance = ScriptableObject.CreateInstance<AndroidLogcatInternalLog>();

            ms_Instance.titleContent = new GUIContent("Internal Log");
            ms_Instance.Show(immediate);
            ms_Instance.Focus();
        }

        /// <summary>
        /// This function should be thread safe.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Log(string message, params object[] args)
        {
            Log(string.Format(message, args));
        }

        public static void Log(string message)
        {
            var timedMessage = AndroidLogcatDispatcher.isMainThread ? "[MainThread]" : "[WorkThread] ";
            timedMessage += DateTime.Now.ToString("HH:mm:ss.ffff") + " " + message;

            lock (ms_LogEntries)
            {
                var rawEntries = timedMessage.Split(new[] { '\n' });
                if (rawEntries.Length == 0)
                    return;

                var start = ms_LogEntries.Count;
                ms_LogEntries.AddRange(new InternalLogEntry[rawEntries.Length]);
                var entries = new InternalLogEntry[rawEntries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    ms_LogEntries[start + i] = new InternalLogEntry()
                    {
                        Entry = rawEntries[i]
                    };
                }
                
                if (ms_LogEntries.Count > kMaxInternalLogMessages)
                    ms_LogEntries.RemoveRange(0, ms_LogEntries.Count - kMaxInternalLogMessages);

                s_RecalculateMaxEntryWidth = true;
            }

            Console.WriteLine("[Logcat] " + timedMessage);
            if (AndroidLogcatDispatcher.isMainThread && ms_Instance != null)
            {
                ms_Instance.m_ScrollPosition = new Vector2(ms_Instance.m_ScrollPosition.x, float.MaxValue);
                ms_Instance.Repaint();
            }
        }

        public void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;
            ms_Instance = this;
        }

        public void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            DoEntriesGUI();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Entries: {ms_LogEntries.Count} / {kMaxInternalLogMessages}");
            if (GUILayout.Button("Clear"))
            {
                lock (ms_LogEntries)
                {
                    ms_LogEntries.Clear();
                    s_RecalculateMaxEntryWidth = true;
                }
            }
            if (Unsupported.IsDeveloperMode())
                DoDebuggingGUI();
            GUILayout.EndHorizontal();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);

            var logHeight = AndroidLogcatStyles.internalLogStyle.fixedHeight;
            var screenWidth = Screen.width;
            var e = Event.current;

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, true, true);
            GUILayoutUtility.GetRect(m_MaxEntryWidth, ms_LogEntries.Count * logHeight);
            GUILayout.EndScrollView();

            if (e.type == EventType.Repaint)
            {
                m_ScrollArea = GUILayoutUtility.GetLastRect();
            }


            if (e.type == EventType.MouseDown)
            {
                if (e.button == 0)
                {
                    var idx = (int)((e.mousePosition.y - m_ScrollArea.y + (int)(m_ScrollPosition.y / logHeight) * logHeight) / (float)AndroidLogcatStyles.internalLogStyle.fixedHeight);
                    if (idx < ms_LogEntries.Count)
                    {
                        if (!e.HasCtrlOrCmdModifier())
                        {
                            foreach (var ee in ms_LogEntries)
                                ee.Selected = false;
                        }

                        ms_LogEntries[idx].Selected = !ms_LogEntries[idx].Selected;
                        Repaint();
                    }
                }
                else if (e.button == 1)
                {
                    var menuItems = new[] { new GUIContent("Copy"), new GUIContent("Select All") };
                    EditorUtility.DisplayCustomMenu(new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0),
                        menuItems.ToArray(), -1, MenuSelection, null);
                }
            }
        }

        private void DoEntriesGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var logHeight = AndroidLogcatStyles.internalLogStyle.fixedHeight;
            var startIdx = (int)(m_ScrollPosition.y / logHeight);
            var displayCount = m_ScrollArea.height / logHeight;
            var style = AndroidLogcatStyles.internalLogStyle;

            if (s_RecalculateMaxEntryWidth)
            {
                s_RecalculateMaxEntryWidth = false;
                m_MaxEntryWidth = 0;
                for (int i = 0; i < ms_LogEntries.Count; i++)
                {
                    m_MaxEntryWidth = Mathf.Max(m_MaxEntryWidth, style.CalcSize(new GUIContent(ms_LogEntries[i].Entry)).x);
                }
            }

            for (int i = 0; i < ms_LogEntries.Count; i++)
            {
                if (i < startIdx || i >= startIdx + displayCount)
                    continue;
                var ee = ms_LogEntries[i];
                var eerc = new Rect(-m_ScrollPosition.x, m_ScrollArea.y + (i - startIdx) * logHeight, m_MaxEntryWidth, logHeight);
                style.Draw(eerc, ee.Entry, false, false, ee.Selected, false);
            }
        }

        void DoDebuggingGUI()
        {
            var entryCount = 1000;
            GUILayout.Label("Debugging Options:");
            if (GUILayout.Button($"Add {entryCount} entries"))
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new System.Random(0);
                for (int i = 0; i < entryCount; i++)
                {
                    var randomText = new String(Enumerable.Repeat(chars, UnityEngine.Random.Range(50, 100))
                        .Select(s => s[random.Next(s.Length)]).ToArray());
                    Log($"[{i}] {randomText}");
                }
            }
        }

        private void MenuSelection(object userData, string[] options, int selected)
        {
            switch (selected)
            {
                // Copy
                case 0:
                    var builder = new StringBuilder();
                    foreach (var ee in ms_LogEntries)
                    {
                        if (!ee.Selected)
                            continue;
                        builder.AppendLine(ee.Entry);
                    }

                    EditorGUIUtility.systemCopyBuffer = builder.ToString();
                    break;
                // Select All
                case 1:
                    foreach (var ee in ms_LogEntries)
                        ee.Selected = true;
                    break;
            }
        }
    }
}
