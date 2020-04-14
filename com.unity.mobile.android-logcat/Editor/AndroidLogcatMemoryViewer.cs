#if PLATFORM_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using System.Text;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal enum MemoryViewerState
    {
        Hidden,
        Auto,
        Manual
    }

    internal class AndroidLogcatMemoryViewer
    {
        class AndroidLogcatQueryMemoryInput : IAndroidLogcatTaskInput
        {
            internal ADB adb;
            internal int packageProcessId;
            internal string packageName;
            internal string deviceId;
        }

        class AndroidLogcatQueryMemoryResult : IAndroidLogcatTaskResult
        {
            internal int packageProcessId;
            internal string packageName;
            internal string contents;
            internal string deviceId;
        }

        private EditorWindow m_Parent;
        private IAndroidLogcatRuntime m_Runtime;
        private Material m_Material;

        const int kMaxEntries = 300;
        const int k16MB = 16 * 1024 * 1024;
        const float kMinMemoryWindowHeight = 255.0f;
        private AndroidMemoryStatistics[] m_Entries = new AndroidMemoryStatistics[kMaxEntries];
        private AndroidMemoryStatistics m_LastAllocatedEntry = new AndroidMemoryStatistics();
        private int m_CurrentEntry = 0;
        private int m_EntryCount = 0;
        private int m_UpperMemoryBoundry = 32 * 1024 * 1024;
        private int m_RequestsInQueue;
        private int m_SelectedEntry;
        [SerializeField]
        private float m_MemoryWindowHeight;

        private bool m_SplitterDragging;
        private float m_SplitterStart;
        private float m_OldMemoryWindowHeight;

        private MemoryType[] m_OrderMemoryTypesPSS = new[]
        {
            MemoryType.NativeHeap,
            MemoryType.JavaHeap,
            MemoryType.Code,
            MemoryType.Stack,
            MemoryType.Graphics,
            MemoryType.PrivateOther,
            MemoryType.System
        };

        private MemoryType[] m_OrderMemoryTypesHeap = new[]
        {
            MemoryType.NativeHeap,
            MemoryType.JavaHeap,
        };

        [SerializeField]
        private bool[] m_MemoryTypeEnabled;

        [SerializeField]
        private MemoryGroup m_MemoryGroup = MemoryGroup.HeapAlloc;

        private string m_ExpectedDeviceId;
        private AndroidLogcatConsoleWindow.PackageInformation m_ExpectedPackageFromRequest;

        [SerializeField]
        private MemoryViewerState m_MemoryViewerState;

        private MemoryType[] GetOrderMemoryTypes()
        {
            return m_MemoryGroup == MemoryGroup.ProportionalSetSize ? m_OrderMemoryTypesPSS : m_OrderMemoryTypesHeap;
        }

        Dictionary<MemoryType, Color> m_MemoryTypeColors = new Dictionary<MemoryType, Color>();

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

            m_SplitterStart = 0;
            m_SplitterDragging = false;
            m_MemoryViewerState = MemoryViewerState.Auto;

            m_MemoryTypeColors[MemoryType.NativeHeap] = Color.red;
            m_MemoryTypeColors[MemoryType.JavaHeap] = Color.yellow;
            m_MemoryTypeColors[MemoryType.Code] = Color.blue;
            m_MemoryTypeColors[MemoryType.Stack] = Color.cyan;
            m_MemoryTypeColors[MemoryType.Graphics] = Color.green;
            m_MemoryTypeColors[MemoryType.PrivateOther] = Color.grey;
            m_MemoryTypeColors[MemoryType.System] = Color.magenta;
            m_MemoryTypeColors[MemoryType.Total] = Color.white;

            ValidateSettings();

            ClearEntries();
        }

        internal MemoryViewerState State
        {
            set
            {
                m_MemoryViewerState = value;
            }

            get
            {
                return m_MemoryViewerState;
            }
        }

        /// <summary>
        /// Validate serialized settings here
        /// </summary>
        internal void ValidateSettings()
        {
            var allMemoryTypes = (MemoryType[])Enum.GetValues(typeof(MemoryType));

            if (m_MemoryTypeEnabled == null || m_MemoryTypeEnabled.Length != allMemoryTypes.Length)
            {
                m_MemoryTypeEnabled = new bool[allMemoryTypes.Length];
                for (int i = 0; i < m_MemoryTypeEnabled.Length; i++)
                    m_MemoryTypeEnabled[i] = true;
            }

            if (m_MemoryWindowHeight < kMinMemoryWindowHeight)
                m_MemoryWindowHeight = 300.0f;
        }

        internal void ClearEntries()
        {
            m_SelectedEntry = -1;
            m_EntryCount = 0;
            m_CurrentEntry = 0;
            m_UpperMemoryBoundry = 32 * 1024 * 1024;
            m_ExpectedPackageFromRequest = null;
            m_ExpectedDeviceId = null;
        }

        internal void QueueMemoryRequest(string deviceId, AndroidLogcatConsoleWindow.PackageInformation package)
        {
            m_ExpectedDeviceId = deviceId;
            m_ExpectedPackageFromRequest = package;
            if (m_ExpectedPackageFromRequest == null || !m_ExpectedPackageFromRequest.IsAlive() || m_ExpectedDeviceId == null)
                return;
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
                    packageProcessId = m_ExpectedPackageFromRequest.processId,
                    packageName = m_ExpectedPackageFromRequest.name,
                    deviceId = deviceId
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

            var cmd = "-s " + workInput.deviceId + " shell dumpsys meminfo " + workInput.packageName;
            AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

            string outputMsg = string.Empty;
            try
            {
                outputMsg = adb.Run(new[] { cmd }, "Failed to query memory for " + workInput.packageName);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Failed to query memory: \n" + ex.Message);
            }
            var result = new AndroidLogcatQueryMemoryResult();
            result.deviceId = workInput.deviceId;
            result.packageName = workInput.packageName;
            result.packageProcessId = workInput.packageProcessId;
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
            // Set the upper boundry depending on total memory from all groups
            foreach (var m in (MemoryGroup[])Enum.GetValues(typeof(MemoryGroup)))
            {
                var totalMemory = lastMemoryStatistics.GetValue(m, MemoryType.Total);

                // 1.1f ensures that there's a small gap between graph an upper windows boundry
                while (totalMemory * 1.1f > m_UpperMemoryBoundry)
                    m_UpperMemoryBoundry += k16MB;
            }
        }

        private void InjectFakeMemoryStatistics(int totalMemory)
        {
            var stats = AllocateMemoryStatistics();
            stats.SetPSSFakeData(totalMemory, totalMemory);
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
            if (m_ExpectedPackageFromRequest == null || m_ExpectedDeviceId == null)
                return;

            if (memoryResult.packageProcessId != m_ExpectedPackageFromRequest.processId ||
                memoryResult.deviceId != m_ExpectedDeviceId ||
                string.IsNullOrEmpty(memoryResult.contents))
                return;

            if (memoryResult.contents.Contains("No process found for:"))
            {
                m_ExpectedPackageFromRequest.SetExited();
                m_Parent.Repaint();
                return;
            }

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
            Color color;
            if (m_MemoryTypeColors.TryGetValue(type, out color))
                return color;
            throw new NotImplementedException(type.ToString());
        }

        private void DoMemoryToggle(MemoryType type)
        {
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            Color oldColor = GUI.backgroundColor;
            var memory = m_ExpectedPackageFromRequest == null ? "0" : IntToSizeString(m_LastAllocatedEntry.GetValue(m_MemoryGroup, type));
            var name = String.Format("{0} ({1})", type, memory);
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
                        m_MemoryWindowHeight = Math.Max(m_OldMemoryWindowHeight + m_SplitterStart - e.mousePosition.y, kMinMemoryWindowHeight);

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
            if (m_MemoryViewerState == MemoryViewerState.Hidden)
                return;

            var splitterRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));
            DoSplitter(splitterRect);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(170), GUILayout.Height(m_MemoryWindowHeight));

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Group:");
            m_MemoryGroup = (MemoryGroup)EditorGUILayout.EnumPopup(m_MemoryGroup);
            GUILayout.EndHorizontal();

            foreach (var m in GetOrderMemoryTypes())
            {
                DoMemoryToggle(m);
            }

            DoMemoryToggle(MemoryType.Total);

            if (m_MemoryViewerState == MemoryViewerState.Manual)
            {
                GUILayout.Space(10);
                if (GUILayout.Button("Capture", EditorStyles.miniButton))
                    QueueMemoryRequest(m_ExpectedDeviceId, m_ExpectedPackageFromRequest);
            }

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

            if (m_ExpectedPackageFromRequest == null)
                EditorGUI.HelpBox(size, "Select a package", MessageType.Info);

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private int AggregateMemorySize(AndroidMemoryStatistics stats, MemoryType type)
        {
            int total = 0;
            MemoryType[] types = GetOrderMemoryTypes();
            for (int i = types.Length - 1; i >= 0; i--)
            {
                if (types[i] == type)
                    return total;
                if (!m_MemoryTypeEnabled[i])
                    continue;
                total += stats.GetValue(m_MemoryGroup, types[i]);
            }

            throw new Exception("Unhandled memory type: " + type);
        }

        private void DoEntriesGUI(Rect windowSize)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            m_Material.SetPass(0);

            // Triangle strip
            // 0  2
            // | /|
            // |/ |
            // 1  3
            var width = GetEntryWidth(windowSize);
            var multiplier = windowSize.height / m_UpperMemoryBoundry;
            var t = windowSize.y;
            var b = windowSize.height + windowSize.y;
            var xOffset = windowSize.x + windowSize.width - (m_EntryCount - 1) * width;

            foreach (var m in GetOrderMemoryTypes())
            {
                if (!m_MemoryTypeEnabled[(int)m])
                    continue;
                GL.Begin(GL.TRIANGLE_STRIP);
                GL.Color(GetMemoryColor(m));

                for (int i = 0; i < m_EntryCount; i++)
                {
                    var idx = ResolveEntryIndex(i);
                    var agr = AggregateMemorySize(m_Entries[idx], m);
                    var val = m_Entries[idx].GetValue(m_MemoryGroup, m);
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

            int enabledCount = 0;
            foreach (var m in GetOrderMemoryTypes())
            {
                if (!m_MemoryTypeEnabled[(int)m])
                    continue;
                info.AppendLine(m.ToString() + " : " + IntToSizeString(m_Entries[idx].GetValue(m_MemoryGroup, m)));
                enabledCount++;
            }

            info.AppendLine("Total: " + IntToSizeString(m_Entries[idx].GetValue(m_MemoryGroup, MemoryType.Total)));

            const float kInfoWidth = 150;
            var infoX = x + 5;
            if (infoX + kInfoWidth > windowSize.x + windowSize.width)
                infoX -= kInfoWidth + 10;
            var rc = new Rect(infoX, t + 10, kInfoWidth, 19 * enabledCount + 30);
            GUI.Box(rc, GUIContent.none, GUI.skin.window);
            GUI.Label(rc, info.ToString());
        }
    }
}

#endif
