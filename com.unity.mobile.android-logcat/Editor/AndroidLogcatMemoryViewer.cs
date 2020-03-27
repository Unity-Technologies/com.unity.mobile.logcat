#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatMemoryViewer
    {
        class AndroidMemoryStatistics
        {
            private Dictionary<string, int> m_AppSummary = new Dictionary<string, int>();

            public int NativeHeap { get { return GetValue("native heap"); } }
            public int JavaHeap { get { return GetValue("java heap"); } }
            public int Code { get { return GetValue("code"); } }
            public int Stack { get { return GetValue("stack"); } }
            public int Graphics { get { return GetValue("graphics"); } }
            public int PrivateOther { get { return GetValue("private other"); } }
            public int System { get { return GetValue("system"); } }
            public int Total { get { return GetValue("total"); } }

            private int GetValue(string key)
            {
                int value;
                if (m_AppSummary.TryGetValue(key, out value))
                    return value;
                return 0;
            }

            void ParseAppSummary(string appSummary)
            {
                string pattern = @"([\w\s]+):\s+(\d+)";

                Regex r = new Regex(pattern, RegexOptions.IgnoreCase);
                MatchCollection matches = r.Matches(appSummary);
                int dummy;
                foreach (Match match in matches)
                {
                    var name = match.Groups[1].Value.Trim().ToLower();
                    var sizeInKBytes = Int32.Parse(match.Groups[2].Value);
                    m_AppSummary[name] = sizeInKBytes * 1024;
                }

                if (!m_AppSummary.TryGetValue("native heap", out dummy))
                {
                    throw new Exception("Failed to find native heap size in\n" + appSummary);
                }
            }

            /// <summary>
            /// Parses contents from command 'adb shell dumpsys meminfo package_name'
            /// </summary>
            /// <param name="contents"></param>
            /// <returns></returns>
            public void Parse(string contents)
            {
                string[] sections = contents.Split(new string[] { "MEMINFO", "App Summary", "Objects", "SQL" }, StringSplitOptions.RemoveEmptyEntries);
                if (sections.Length != 5)
                    throw new Exception("Expected 5 sections when parsing memory statistics:\n" + contents);
                ParseAppSummary(sections[2]);
            }
        }

        class AndroidLogcatQueryMemoryInput : IAndroidLogcatTaskInput
        {
            internal ADB adb;
            internal string packageName;
        }

        class AndroidLogcatQueryMemoryResult : IAndroidLogcatTaskResult
        {
            internal string contents;
        }

        private EditorWindow m_Parent;
        private IAndroidLogcatRuntime m_Runtime;
        private Material m_Material;
        private string m_PackageName;
        private Rect m_WindowSize;


        const int kMaxEntries = 500;
        private AndroidMemoryStatistics[] m_Entries = new AndroidMemoryStatistics[kMaxEntries];
        private int m_CurrentEntry = 0;
        private int m_EntryCount = 0;
        private int m_MaxMemorySize = int.MinValue;
        private int m_MinMemorySize = int.MaxValue;
        private int m_RequestsInQueue;

        public AndroidLogcatMemoryViewer(EditorWindow parent, string packageName)
        {
            m_Parent = parent;
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Material = (Material)EditorGUIUtility.LoadRequired("SceneView/HandleLines.mat");
            m_PackageName = packageName;

            for (int i = 0; i < kMaxEntries; i++)
                m_Entries[i] = new AndroidMemoryStatistics();

            m_RequestsInQueue = 0;
        }

        public void QueueMemoryRequest()
        {
            // Don't make a memory request, if previous requests haven't finished yet
            // Otherwise async queue will grow bigger and bigger
            const int kMaxRequestsInQueue = 3;
            if (m_RequestsInQueue > kMaxRequestsInQueue)
                return;
            m_RequestsInQueue++;
            m_Runtime.Dispatcher.Schedule(
                new AndroidLogcatQueryMemoryInput()
                {
                    adb = ADB.GetInstance(),
                    packageName = m_PackageName
                },
                QueryMemoryAsync,
                IntegrateQueryMemory,
                false);
        }

        private static string IntToSizeString(int value)
        {
            if (value < 0)
                return "unknown";
            float val = (float)value;
            string[] scale = new string[] { "TB", "GB", "MB", "KB", "Bytes" };
            int idx = scale.Length - 1;
            while (val > 1000.0f && idx >= 0)
            {
                val /= 1000f;
                idx--;
            }

            if (idx < 0)
                return "<error>";

            return string.Format("{0:#.##} {1}", val, scale[idx]);
        }

        private static IAndroidLogcatTaskResult QueryMemoryAsync(IAndroidLogcatTaskInput input)
        {
            var workInput = ((AndroidLogcatQueryMemoryInput)input);
            var adb = workInput.adb;

            if (adb == null)
                throw new NullReferenceException("ADB interface has to be valid");

            var cmd = "shell dumpsys meminfo " + workInput.packageName;
            AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

            var outputMsg = adb.Run(new[] { cmd }, "Failed to query memory for " + workInput.packageName);
            var result = new AndroidLogcatQueryMemoryResult();
            result.contents = outputMsg;
            AndroidLogcatInternalLog.Log(outputMsg);

            return result;
        }

        private void IntegrateQueryMemory(IAndroidLogcatTaskResult result)
        {
            m_RequestsInQueue--;
            if (m_RequestsInQueue < 0)
            {
                m_RequestsInQueue = 0;
                throw new Exception("Receiving more memory results than requested ?");
            }
            var memoryResult = (AndroidLogcatQueryMemoryResult)result;
            var stats = m_Entries[m_CurrentEntry++];
            stats.Parse(memoryResult.contents);

            if (m_CurrentEntry >= kMaxEntries)
                m_CurrentEntry = 0;
            m_EntryCount = Math.Min(m_EntryCount + 1, kMaxEntries);
            var totalMemory = stats.Total;
            if (totalMemory > m_MaxMemorySize)
                m_MaxMemorySize = totalMemory;
            if (totalMemory < m_MinMemorySize)
                m_MinMemorySize = totalMemory;

            m_Parent.Repaint();
        }

        internal void DoGUI()
        {
            GUILayout.Label("Total Memory: " + IntToSizeString(m_MaxMemorySize));
            GUILayout.Label("Total MemoryMin: " + IntToSizeString(m_MinMemorySize));
            // TODO: handle case where m_MaxMemorySize equals min
            if (m_EntryCount == 0 || m_MinMemorySize > m_MaxMemorySize || m_MaxMemorySize == m_MinMemorySize)
                return;
            var e = Event.current.type;

            m_WindowSize = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.internalLogStyle, GUILayout.Height(400));

            if (e != EventType.Repaint)
                return;
            m_Material.SetPass(0);

            // Triangle strip
            // 0  2,4
            // | /|
            // |/ |
            // 1  3
            var width = m_WindowSize.width / (kMaxEntries - 1);
            var multiplier = m_WindowSize.height / (m_MaxMemorySize - m_MinMemorySize);
            var b = m_WindowSize.height + m_WindowSize.y;
            var xOffset = m_WindowSize.width - (m_EntryCount - 1) * width;

            var a0 = Mathf.Repeat(-1, 3);
            var a1 = Mathf.Repeat(0, 3);
            var a2 = Mathf.Repeat(1, 3);
            var a3 = Mathf.Repeat(2, 3);
            var a4 = Mathf.Repeat(3, 3);
            var a5 = Mathf.Repeat(4, 3);

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(Color.red);
            float y = 0.0f;
            float x = 0.0f;
            for (int i = 0; i < m_EntryCount; i++)
            {
                var idx = (int)Mathf.Repeat(i + m_CurrentEntry - m_EntryCount, kMaxEntries);
                var val = m_Entries[idx].Total - m_MinMemorySize;
                x = xOffset + i * width;
                y = b - multiplier * val;
                GL.Vertex3(x, y, 0);
                GL.Vertex3(x, b, 0);
            }

            GL.End();
        }
    }
}

#endif
