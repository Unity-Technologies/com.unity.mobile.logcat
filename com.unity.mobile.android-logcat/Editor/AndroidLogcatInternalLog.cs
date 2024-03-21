using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatInternalLog : EditorWindow
    {
        class InternalLogEntry
        {
            public bool Selected { get; set; }
            public string Entry { get; set; }
        }

        const int kMaxInternalLogBuffer = 15000;
        static AndroidLogcatInternalLog ms_Instance = null;
        static StringBuilder ms_LogEntries = new StringBuilder();
        static List<InternalLogEntry> ms_LogEntries2 = new List<InternalLogEntry>();

        Vector2 m_ScrollPosition = Vector2.zero;
        Rect m_ScrollArea;
        float m_MaxEntryWidth;

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

            lock (ms_LogEntries2)
            {
                var rawEntries = timedMessage.Split(new[] { '\n' });
                if (rawEntries.Length == 0)
                    return;

                var start = ms_LogEntries2.Count;
                ms_LogEntries2.AddRange(new InternalLogEntry[rawEntries.Length]);
                var entries = new InternalLogEntry[rawEntries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    ms_LogEntries2[start + i] = new InternalLogEntry()
                    {
                        Entry = rawEntries[i]
                    };
                }
                
            }
            lock (ms_LogEntries)
            {
                ms_LogEntries.AppendLine(timedMessage);

                const int MaxTriesToStripBuffer = 30;
                for (int i = 0; i < MaxTriesToStripBuffer; i++)
                {
                    if (ms_LogEntries.Length < kMaxInternalLogBuffer)
                        break;
                    ms_LogEntries.Remove(0, Convert.ToString(ms_LogEntries).Split('\n').FirstOrDefault().Length + 1);
                }  
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


            GUILayout.BeginHorizontal();
            int count;
            lock (ms_LogEntries)
            {
                count = ms_LogEntries2.Count;
            }

            GUILayout.Label("Entries: " + count);
            if (GUILayout.Button("Clear"))
            {
                lock (ms_LogEntries)
                {
                    ms_LogEntries = new StringBuilder();
                    ms_LogEntries2.Clear();
                }
            }

            GUILayout.EndHorizontal();

            var logHeight = AndroidLogcatStyles.internalLogStyle.fixedHeight;
            var screenWidth = Screen.width;
            var e = Event.current;

            if (e.type == EventType.Repaint)
            {
                if (e.type == EventType.Repaint)
                {
                    var startIdx = (int)(m_ScrollPosition.y / logHeight);
                    var displayCount = m_ScrollArea.height / logHeight;
                    var style = AndroidLogcatStyles.internalLogStyle;
                    m_MaxEntryWidth = 0;
                    for (int i = 0; i < ms_LogEntries2.Count; i++)
                    {
                        
                        m_MaxEntryWidth = Mathf.Max(m_MaxEntryWidth, style.CalcSize(new GUIContent(ms_LogEntries2[i].Entry)).x);
                    }
                    for (int i = 0; i < ms_LogEntries2.Count; i++)
                    {
                        if (i < startIdx || i >= startIdx + displayCount)
                            continue;
                        var ee = ms_LogEntries2[i];
                        var eerc = new Rect(-m_ScrollPosition.x, m_ScrollArea.y + i * logHeight - m_ScrollPosition.y, m_MaxEntryWidth, logHeight);
                            style.Draw(eerc, ee.Entry, false, false, ee.Selected, false);
                    }
                }
            }

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, true, true);
            GUILayoutUtility.GetRect(m_MaxEntryWidth, ms_LogEntries2.Count * logHeight);
            GUILayout.EndScrollView();

            if (e.type == EventType.Repaint)
            {
                m_ScrollArea = GUILayoutUtility.GetLastRect();
            }


            if (e.type == EventType.MouseDown)
            {
                if (e.button == 0)
                {
                    var idx = (int)((e.mousePosition.y - m_ScrollArea.y + m_ScrollPosition.y) / (float)AndroidLogcatStyles.internalLogStyle.fixedHeight);
                    if (idx < ms_LogEntries2.Count)
                    {
                        ms_LogEntries2[idx].Selected = !ms_LogEntries2[idx].Selected;
                        Repaint();
                    }
                }
                else if (e.button == 1)
                {
                    var menuItems = new[] { new GUIContent("Copy All") };
                    EditorUtility.DisplayCustomMenu(new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0),
                        menuItems.ToArray(), -1, MenuSelection, null);
                }
            }
        }

        private void MenuSelection(object userData, string[] options, int selected)
        {
            switch (selected)
            {
                // Copy All
                case 0:
                    EditorGUIUtility.systemCopyBuffer = ms_LogEntries.ToString();
                    break;
            }
        }
    }
}
