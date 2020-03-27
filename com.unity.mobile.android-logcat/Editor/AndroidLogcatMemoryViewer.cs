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
        enum MemoryType
        {
            NativeHeap,
            JavaHeap,
            Code,
            Stack,
            Graphics,
            PrivateOther,
            System
        }


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

            public int GetValue(MemoryType type)
            {
                switch(type)
                {
                    case MemoryType.NativeHeap: return NativeHeap;
                    case MemoryType.JavaHeap: return JavaHeap;
                    case MemoryType.Code: return Code;
                    case MemoryType.Stack: return Stack;
                    case MemoryType.Graphics: return Graphics;
                    case MemoryType.PrivateOther: return PrivateOther;
                    case MemoryType.System: return System;
                    default:
                        throw new NotImplementedException(type.ToString());
                }
            }

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

            public void SetFakeData(int totalMemory)
            {
                m_AppSummary["total"] = totalMemory;
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


        const int kMaxEntries = 300;
        const int k16MB = 16 * 1024 * 1024;
        private AndroidMemoryStatistics[] m_Entries = new AndroidMemoryStatistics[kMaxEntries];
        private int m_CurrentEntry = 0;
        private int m_EntryCount = 0;
        private int m_UpperMemoryBoundry = 32 * 1024 * 1024;
        private int m_RequestsInQueue;
        private int m_SelectedEntry;

        public AndroidLogcatMemoryViewer(EditorWindow parent, string packageName)
        {
            m_Parent = parent;
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Material = (Material)EditorGUIUtility.LoadRequired("SceneView/HandleLines.mat");
            m_PackageName = packageName;

            for (int i = 0; i < kMaxEntries; i++)
                m_Entries[i] = new AndroidMemoryStatistics();

            m_RequestsInQueue = 0;
            m_SelectedEntry = -1;

            /*
            // For Debugging purposes
            for (int i = 0; i < kMaxEntries / 2; i++)
            {
                InjectFakeMemoryStatistics((int)(UnityEngine.Random.value * 100.0f));
            }
            //**/
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
            if (value == 0)
                return "0 Bytes";
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

        private AndroidMemoryStatistics AllocateMemoryStatistics()
        {
            var stats = m_Entries[m_CurrentEntry++];
            if (m_CurrentEntry >= kMaxEntries)
                m_CurrentEntry = 0;
            m_EntryCount = Math.Min(m_EntryCount + 1, kMaxEntries);

            if (m_SelectedEntry >= 0 && m_EntryCount == kMaxEntries)
                m_SelectedEntry--;
            return stats;
        }

        private void UpdateGeneralStats(AndroidMemoryStatistics lastMemoryStatistics)
        {
            var totalMemory = lastMemoryStatistics.Total;
            
            // 1.1f ensures that there's a small gap between graph an upper windows boundry
            while (totalMemory * 1.1f > m_UpperMemoryBoundry)
                m_UpperMemoryBoundry += k16MB;
        }

        private void InjectFakeMemoryStatistics(int totalMemory)
        {
            var stats = AllocateMemoryStatistics();
            stats.SetFakeData(totalMemory);
            UpdateGeneralStats(stats);
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

            var stats = AllocateMemoryStatistics();
            stats.Parse(memoryResult.contents);
            UpdateGeneralStats(stats);

            m_Parent.Repaint();
        }

        private float GetEntryWidth()
        {
            return m_WindowSize.width / (kMaxEntries - 1);
        }

        private int ResolveEntryIndex(int entry)
        {
            return (int)Mathf.Repeat(entry + m_CurrentEntry - m_EntryCount, kMaxEntries);
        }

        internal void SelectStats()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && m_WindowSize.Contains(e.mousePosition))
            {
                float wd = GetEntryWidth();
                m_SelectedEntry = (int)((e.mousePosition.x - m_WindowSize.x + wd * 0.5f) / wd);
                // Correct entry for cases where we don't have enough entries to fill the full array
                m_SelectedEntry += m_EntryCount - kMaxEntries; 
                m_Parent.Repaint();
            } 
        }

        internal void DoGUI()
        {
            // GUILayout.Label("Total Memory: " + IntToSizeString(m_UpperMemoryBoundry));
            // Note: GUILayoutUtility.GetRect must be called for Layout event always
            var size = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.internalLogStyle, GUILayout.Height(400));

            if (m_EntryCount == 0)
                return;
            var e = Event.current.type;

            SelectStats();


            if (e == EventType.Repaint)
                m_WindowSize = size;

            DoEntriesGUI();
            DoSelectedStatsGUI();
        }

        private int AggregateMemorySize(AndroidMemoryStatistics stats, MemoryType[] orderedMemoryTypes, MemoryType type)
        {
            int total = 0;
            for (int i = 0; i < orderedMemoryTypes.Length; i++)
            {
                if (orderedMemoryTypes[i] == type)
                    return total;
                total += stats.GetValue(orderedMemoryTypes[i]);
            }

            throw new Exception("Unhandled memory type: " + type);
        }

        private Color GetMemoryColor(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.NativeHeap: return Color.red;
                case MemoryType.JavaHeap: return Color.yellow;
                case MemoryType.Code: return Color.blue;
                case MemoryType.Stack: return Color.cyan;
                case MemoryType.Graphics: return Color.green;
                case MemoryType.PrivateOther: return Color.grey;
                case MemoryType.System: return Color.magenta;
                default:
                    throw new NotImplementedException(type.ToString());
            }
        }

        internal void DoEntriesGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // Last are most important
            MemoryType[] orderedMemoryTypes = new[]
            {
                MemoryType.Code,
                MemoryType.Stack,
                MemoryType.PrivateOther,
                MemoryType.System,
                MemoryType.Graphics,
                MemoryType.JavaHeap,
                MemoryType.NativeHeap,
            };


            m_Material.SetPass(0);

            // Triangle strip
            // 0  2,4
            // | /|
            // |/ |
            // 1  3
            var width = GetEntryWidth();
            var multiplier = m_WindowSize.height / m_UpperMemoryBoundry;
            var t = m_WindowSize.y;
            var b = m_WindowSize.height + m_WindowSize.y;
            var xOffset = m_WindowSize.width - (m_EntryCount - 1) * width;

            foreach (var m in orderedMemoryTypes)
            {
                GL.Begin(GL.TRIANGLE_STRIP);
                GL.Color(GetMemoryColor(m));

                for (int i = 0; i < m_EntryCount; i++)
                {
                    var idx = ResolveEntryIndex(i);
                    var agr = AggregateMemorySize(m_Entries[idx], orderedMemoryTypes, m);
                    var val = m_Entries[idx].GetValue(m);
                    var x = xOffset + i * width;
                    var y1 = b - multiplier * (val + agr);
                    var y2 = b - multiplier * agr;
                    GL.Vertex3(x, y1, 0);
                    GL.Vertex3(x, y2, 0);
                }
                GL.End();
            }  
        }

        internal void DoSelectedStatsGUI()
        {
            if (m_SelectedEntry < 0)
                return;
            var width = GetEntryWidth();
            var x = m_WindowSize.width - (m_EntryCount - 1) * width + m_SelectedEntry * width;
            var x1 = x - 2;
            var x2 = x + 2;
            var t = m_WindowSize.y;
            var b = m_WindowSize.height + m_WindowSize.y;
            if (Event.current.type == EventType.Repaint)
            {
                GL.Begin(GL.QUADS);
                GL.Color(Color.white);
                GL.Vertex3(x1, t, 0);
                GL.Vertex3(x1, b, 0);

                GL.Vertex3(x2, b, 0);
                GL.Vertex3(x2, t, 0);
                GL.End();
            }

            var idx = ResolveEntryIndex(m_SelectedEntry);
            var info = new StringBuilder();
            info.AppendLine("Total: " + IntToSizeString(m_Entries[idx].Total));
            info.AppendLine("Native: " + IntToSizeString(m_Entries[idx].NativeHeap));
            info.AppendLine("Java: " + IntToSizeString(m_Entries[idx].JavaHeap));
            info.AppendLine("Graphics: " + IntToSizeString(m_Entries[idx].Graphics));
            GUI.Label(new Rect(x2 + 5, t, 200, 100), info.ToString());
        }
    }
}

#endif
