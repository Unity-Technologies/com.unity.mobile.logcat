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
        const int MaxInternalLogMessages = 10000;
        static AndroidLogcatInternalLog ms_Instance = null;

        static AndroidLogcatFastListView m_ListView;

        static AndroidLogcatFastListView ListView
        {
            get
            {
                if (m_ListView == null)
                    m_ListView = new AndroidLogcatFastListView(() => AndroidLogcatStyles.internalLogStyle, MaxInternalLogMessages);
                return m_ListView;
            }
        }

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


            var rawEntries = timedMessage.Split(new[] { '\n' });
            ListView.AddEntries(rawEntries);

            Console.WriteLine("[Logcat] " + timedMessage);
            if (AndroidLogcatDispatcher.isMainThread && ms_Instance != null)
            {
                ms_Instance.Repaint();
            }
        }

        public void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;
            ms_Instance = this;
        }

        internal void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Entries: {ListView.Entries.Count()} / {MaxInternalLogMessages}");
            if (GUILayout.Button("Clear"))
            {
                ListView.ClearEntries();
            }
            if (Unsupported.IsDeveloperMode())
                DoDebuggingGUI();
            GUILayout.EndHorizontal();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);

            if (ListView.OnGUI())
                Repaint();
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
    }
}
