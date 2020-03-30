#if PLATFORM_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using System.Text;
using UnityEngine;
using static Unity.Android.Logcat.AndroidLogcatConsoleWindow;

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
            System,
            Total
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
                switch (type)
                {
                    case MemoryType.NativeHeap: return NativeHeap;
                    case MemoryType.JavaHeap: return JavaHeap;
                    case MemoryType.Code: return Code;
                    case MemoryType.Stack: return Stack;
                    case MemoryType.Graphics: return Graphics;
                    case MemoryType.PrivateOther: return PrivateOther;
                    case MemoryType.System: return System;
                    case MemoryType.Total: return Total;
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

            public void Clear()
            {
                m_AppSummary.Clear();
            }

            /// <summary>
            /// Parses contents from command 'adb shell dumpsys meminfo package_name'
            /// </summary>
            /// <param name="contents"></param>
            /// <returns></returns>
            public void Parse(string contents)
            {
                int appSummary = contents.IndexOf("App Summary");
                if (appSummary == -1)
                    throw new Exception("Failed to find App Summary:\n" + contents);
                contents = contents.Substring(appSummary);
                ParseAppSummary(contents);
            }

            public void SetFakeData(int totalMemory)
            {
                m_AppSummary["total"] = totalMemory;
                m_AppSummary["native heap"] = totalMemory;
            }
        }

        class AndroidLogcatQueryMemoryInput : IAndroidLogcatTaskInput
        {
            internal ADB adb;
            internal string packageName;
        }

        class AndroidLogcatQueryMemoryResult : IAndroidLogcatTaskResult
        {
            internal string packageName;
            internal string contents;
        }

        private EditorWindow m_Parent;
        private IAndroidLogcatRuntime m_Runtime;
        private Material m_Material;

        const int kMaxEntries = 300;
        const int k16MB = 16 * 1024 * 1024;
        private AndroidMemoryStatistics[] m_Entries = new AndroidMemoryStatistics[kMaxEntries];
        private AndroidMemoryStatistics m_LastAllocatedEntry = new AndroidMemoryStatistics();
        private int m_CurrentEntry = 0;
        private int m_EntryCount = 0;
        private int m_UpperMemoryBoundry = 32 * 1024 * 1024;
        private int m_RequestsInQueue;
        private int m_SelectedEntry;
        private float m_MemoryWindowHeight;

        private bool m_SplitterDragging;
        private float m_SplitterStart;
        private float m_OldMemoryWindowHeight;

        private MemoryType[] m_OrderMemoryTypes = ((MemoryType[])Enum.GetValues(typeof(MemoryType))).Where(m => m != MemoryType.Total).ToArray();
        private bool[] m_MemoryTypeEnabled;

        private string m_ExpectedPackageNameFromRequest;

        public AndroidLogcatMemoryViewer(EditorWindow parent)
        {
            m_Parent = parent;
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Material = (Material)EditorGUIUtility.LoadRequired("SceneView/HandleLines.mat");

            for (int i = 0; i < kMaxEntries; i++)
                m_Entries[i] = new AndroidMemoryStatistics();

            m_RequestsInQueue = 0;

            /*
            // For Debugging purposes
            for (int i = 0; i < kMaxEntries; i++)
            {
                InjectFakeMemoryStatistics((int)(UnityEngine.Random.value * k16MB * 2.0f));
            }
            //**/

            m_MemoryWindowHeight = 300;
            m_SplitterStart = 0;
            m_SplitterDragging = false;

            m_MemoryTypeEnabled = new bool[m_OrderMemoryTypes.Length];
            for (int i = 0; i < m_MemoryTypeEnabled.Length; i++)
                m_MemoryTypeEnabled[i] = true;

            ClearEntries();
        }

        internal void ClearEntries()
        {
            m_SelectedEntry = -1;
            m_EntryCount = 0;
            m_CurrentEntry = 0;
            m_UpperMemoryBoundry = 32 * 1024 * 1024;
            m_ExpectedPackageNameFromRequest = string.Empty;
        }

        internal void QueueMemoryRequest(PackageInformation package)
        {
            if (package == null || string.IsNullOrEmpty(package.name))
            {
                m_ExpectedPackageNameFromRequest = string.Empty;
                return;
            }

            m_ExpectedPackageNameFromRequest = package.name;
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
                    packageName = m_ExpectedPackageNameFromRequest
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
            result.packageName = workInput.packageName;
            result.contents = outputMsg;
            //AndroidLogcatInternalLog.Log(outputMsg);

            return result;
        }

        private AndroidMemoryStatistics AllocateMemoryStatistics()
        {
            m_LastAllocatedEntry = m_Entries[m_CurrentEntry++];
            if (m_CurrentEntry >= kMaxEntries)
                m_CurrentEntry = 0;
            m_EntryCount = Math.Min(m_EntryCount + 1, kMaxEntries);

            if (m_SelectedEntry >= 0 && m_EntryCount == kMaxEntries)
                m_SelectedEntry--;
            return m_LastAllocatedEntry;
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
            // When selecting a new package, there might be still few requests for other packages running on other threads
            // Ignore those
            if (!memoryResult.packageName.Equals(m_ExpectedPackageNameFromRequest))
                return;

            var stats = AllocateMemoryStatistics();
            try
            {
                stats.Parse(memoryResult.contents);
            }
            catch (Exception ex)
            {
                stats.Clear();
                Debug.LogError(ex.Message);
            }
            UpdateGeneralStats(stats);

            m_Parent.Repaint();
        }

        private float GetEntryWidth(Rect windowSize)
        {
            return windowSize.width / (kMaxEntries - 1);
        }

        private int ResolveEntryIndex(int entry)
        {
            return (int)Mathf.Repeat(entry + m_CurrentEntry - m_EntryCount, kMaxEntries);
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
                case MemoryType.Total: return Color.white;
                default:
                    throw new NotImplementedException(type.ToString());
            }
        }

        private void DoMemoryToggle(MemoryType type)
        {
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            Color oldColor = GUI.backgroundColor;
            var name = String.Format("{0} ({1})", type, IntToSizeString(m_LastAllocatedEntry.GetValue(type)));
            if (type == MemoryType.Total)
            {
                GUI.backgroundColor = Color.white;
                GUILayout.Toggle(true, name, AndroidLogcatStyles.kSeriesLabel);
            }
            else
            {
                var enabled = m_MemoryTypeEnabled[(int)type];
                GUI.backgroundColor = enabled ? GetMemoryColor(type) : Color.black;
                m_MemoryTypeEnabled[(int)type] = GUILayout.Toggle(enabled, name, AndroidLogcatStyles.kSeriesLabel);
            }
            GUI.backgroundColor = oldColor;
            GUILayout.EndHorizontal();
        }

        private bool DoSplitter(Rect splitterRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(e.mousePosition))
                    {
                        m_SplitterDragging = true;
                        m_OldMemoryWindowHeight = m_MemoryWindowHeight;
                        m_SplitterStart = e.mousePosition.y;
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.MouseDrag:
                case EventType.MouseUp:
                    if (m_SplitterDragging)
                    {
                        m_MemoryWindowHeight = Math.Max(m_OldMemoryWindowHeight + m_SplitterStart - e.mousePosition.y, 200.0f);

                        if (e.type == EventType.MouseUp)
                        {
                            m_SplitterDragging = false;
                            m_SplitterStart = 0.0f;
                        }
                        e.Use();
                        return true;
                    }
                    break;
            }

            return false;
        }

        internal void DoGUI()
        {
            var splitterRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));
            DoSplitter(splitterRect);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(170), GUILayout.Height(m_MemoryWindowHeight));

            foreach (var m in m_OrderMemoryTypes)
            {
                DoMemoryToggle(m);
            }

            DoMemoryToggle(MemoryType.Total);

            GUILayout.EndVertical();
            var rc = GUILayoutUtility.GetLastRect();


            GUILayout.BeginVertical();
            // Note: GUILayoutUtility.GetRect must be called for Layout event always
            var size = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.internalLogStyle, GUILayout.Height(m_MemoryWindowHeight));

            size.height -= 4;
            if (m_EntryCount > 0)
            {
                DoEntriesGUI(size);
                DoSelectedStatsGUI(size);
            }

            GUI.Box(new Rect(rc.x + 4, size.y, rc.width - 4, size.height), GUIContent.none, EditorStyles.helpBox);
            GUI.Box(new Rect(size.x, size.y, size.width, size.height), GUIContent.none, EditorStyles.helpBox);

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private int AggregateMemorySize(AndroidMemoryStatistics stats, MemoryType type)
        {
            int total = 0;
            for (int i = m_OrderMemoryTypes.Length - 1; i >= 0; i--)
            {
                if (m_OrderMemoryTypes[i] == type)
                    return total;
                if (!m_MemoryTypeEnabled[i])
                    continue;
                total += stats.GetValue(m_OrderMemoryTypes[i]);
            }

            throw new Exception("Unhandled memory type: " + type);
        }

        private void DoEntriesGUI(Rect windowSize)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            m_Material.SetPass(0);

            // Triangle strip
            // 0  2,4
            // | /|
            // |/ |
            // 1  3
            var width = GetEntryWidth(windowSize);
            var multiplier = windowSize.height / m_UpperMemoryBoundry;
            var t = windowSize.y;
            var b = windowSize.height + windowSize.y;
            var xOffset = windowSize.x + windowSize.width - (m_EntryCount - 1) * width;

            foreach (var m in m_OrderMemoryTypes)
            {
                if (!m_MemoryTypeEnabled[(int)m])
                    continue;
                GL.Begin(GL.TRIANGLE_STRIP);
                GL.Color(GetMemoryColor(m));

                for (int i = 0; i < m_EntryCount; i++)
                {
                    var idx = ResolveEntryIndex(i);
                    var agr = AggregateMemorySize(m_Entries[idx], m);
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

        private void DoSelectedStatsGUI(Rect windowSize)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && windowSize.Contains(e.mousePosition))
            {
                float wd = GetEntryWidth(windowSize);
                m_SelectedEntry = (int)((e.mousePosition.x - windowSize.x + wd * 0.5f) / wd);
                // Correct entry for cases where we don't have enough entries to fill the full array
                m_SelectedEntry += m_EntryCount - kMaxEntries;
                m_Parent.Repaint();
            }

            if (m_SelectedEntry < 0)
                return;
            var width = GetEntryWidth(windowSize);
            var x = windowSize.x + windowSize.width - (m_EntryCount - 1) * width + m_SelectedEntry * width;
            var t = windowSize.y;
            var b = windowSize.height + windowSize.y;
            if (e.type == EventType.Repaint)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.white);
                GL.Vertex3(x, t, 0);
                GL.Vertex3(x, b, 0);
                GL.End();
            }

            var idx = ResolveEntryIndex(m_SelectedEntry);
            var info = new StringBuilder();
            info.AppendLine("Total: " + IntToSizeString(m_Entries[idx].Total));

            foreach (var m in m_OrderMemoryTypes)
            {
                if (!m_MemoryTypeEnabled[(int)m])
                    continue;
                info.AppendLine(m.ToString() + " : " + IntToSizeString(m_Entries[idx].GetValue(m)));
            }

            const float kInfoWidth = 150;
            var infoX = x + 5;
            if (infoX + kInfoWidth > windowSize.x + windowSize.width)
                infoX -= kInfoWidth + 10;
            var rc = new Rect(infoX, t + 10, kInfoWidth, 150);
            GUI.Box(rc, GUIContent.none, GUI.skin.window);
            GUI.Label(rc, info.ToString());
        }
    }
}

#endif
