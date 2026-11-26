using System;
using System.Collections.Generic;
using UnityEditor;
using System.Text;
using UnityEngine;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatMemoryViewerState
    {
        public float MemoryWindowWidth;
        public bool[] MemoryTypeEnabled;
        public MemoryGroup MemoryGroup = MemoryGroup.HeapAlloc;
        public bool AutoCapture = true;
    }

    internal class AndroidLogcatMemoryViewer
    {
        enum SplitterDragging
        {
            None,
            Horizontal,
            Vertical
        }

        class AndroidLogcatQueryMemoryInput : IAndroidLogcatTaskInput
        {
            internal AndroidBridge.ADB adb;
            internal int packageProcessId;
            internal string packageName;
            internal string deviceId;
            internal bool queryMeminfo;
        }

        class AndroidLogcatQueryMemoryResult : IAndroidLogcatTaskResult
        {
            internal int packageProcessId;
            internal string packageName;
            internal string contents;
            internal string procStatusContents;
            internal string deviceId;
        }

        /// <summary>
        /// Ring buffer of memory captures, oldest first.
        /// </summary>
        class MemoryHistory
        {
            private readonly AndroidMemoryStatistics[] m_Entries = new AndroidMemoryStatistics[kMaxEntries];
            private int m_CurrentEntry;

            internal int Count { get; private set; }
            internal int SelectedEntry { get; set; }
            internal AndroidMemoryStatistics LastEntry { get; private set; }

            internal MemoryHistory()
            {
                for (int i = 0; i < kMaxEntries; i++)
                    m_Entries[i] = new AndroidMemoryStatistics();
                Clear();
            }

            internal void Clear()
            {
                SelectedEntry = -1;
                Count = 0;
                m_CurrentEntry = 0;
                LastEntry = new AndroidMemoryStatistics();
            }

            internal AndroidMemoryStatistics Allocate()
            {
                LastEntry = m_Entries[m_CurrentEntry++];
                // Entries are reused, clear values from an older capture
                LastEntry.Clear();
                if (m_CurrentEntry >= kMaxEntries)
                    m_CurrentEntry = 0;
                Count = Math.Min(Count + 1, kMaxEntries);

                if (SelectedEntry >= 0 && Count == kMaxEntries)
                    SelectedEntry--;
                return LastEntry;
            }

            /// <summary>
            /// Returns capture by its index, 0 is the oldest one, Count - 1 the latest one.
            /// </summary>
            internal AndroidMemoryStatistics this[int entry] => m_Entries[(int)Mathf.Repeat(entry + m_CurrentEntry - Count, kMaxEntries)];
        }

        private EditorWindow m_Parent;
        private AndroidLogcatRuntimeBase m_Runtime;
        private Material m_Material;

        const int kMaxEntries = 300;
        // Printed by dumpsys meminfo when the process doesn't exist
        const string kProcessNotFound = "No process found for:";
        const float kMinMemoryWindowHeight = 255.0f;
        const float kMaxMemoryWindowHeight = 400.0f;
        const float kMinMemoryWindowWidth = 170.0f;
        const float kMaxMemoryWindowWidth = 500.0f;
        // Captures from dumpsys meminfo, used by every group except Anonymous RSS + Swap.
        // Only added when dumpsys meminfo was run, so these groups don't show gaps for captures without it.
        private MemoryHistory m_MeminfoHistory = new MemoryHistory();
        // Captures from /proc/<pid>/status, used by Anonymous RSS + Swap group. Added on every capture.
        private MemoryHistory m_ProcStatusHistory = new MemoryHistory();
        private UInt64 m_UpperMemoryBoundry = 32 * AndroidMemoryStatistics.kOneMegaByte;
        private int m_RequestsInQueue;

        Splitter m_HorizontalSplitter;
        Splitter m_VerticalSplitter;

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

        private MemoryType[] m_OrderMemoryTypesAnonymousRSSAndSwap = new[]
        {
            MemoryType.AnonymousRSS,
            MemoryType.Swap,
        };

        private IAndroidLogcatDevice m_ExpectedDevice;
        private ProcessInformation m_ExpectedProcessFromRequest;
        private AndroidLogcatMemoryViewerState m_State;
        private string m_LastError;

        private MemoryType[] GetOrderMemoryTypes()
        {
            switch (m_State.MemoryGroup)
            {
                case MemoryGroup.ProportionalSetSize:
                    return m_OrderMemoryTypesPSS;
                case MemoryGroup.AnonymousRSSAndSwap:
                    return m_OrderMemoryTypesAnonymousRSSAndSwap;
                default:
                    return m_OrderMemoryTypesHeap;
            }
        }

        private MemoryHistory ActiveHistory => m_State.MemoryGroup == MemoryGroup.AnonymousRSSAndSwap ? m_ProcStatusHistory : m_MeminfoHistory;

        Dictionary<MemoryType, Color> m_MemoryTypeColors = new Dictionary<MemoryType, Color>();

        public AndroidLogcatMemoryViewer(EditorWindow parent, AndroidLogcatRuntimeBase runtime)
        {
            m_Parent = parent;
            m_Runtime = runtime;
            m_Material = (Material)EditorGUIUtility.LoadRequired("SceneView/HandleLines.mat");
            m_State = m_Runtime.UserSettings.MemoryViewerState;

            m_RequestsInQueue = 0;

            /*
            // For Debugging purposes
            for (int i = 0; i < kMaxEntries; i++)
            {
                InjectFakeMemoryStatistics((int)(UnityEngine.Random.value * k16MB * 2.0f));
            }
            //**/

            m_HorizontalSplitter = new Splitter(Splitter.SplitterType.Horizontal, kMinMemoryWindowWidth, kMaxMemoryWindowWidth);
            m_VerticalSplitter = new Splitter(Splitter.SplitterType.Vertical, kMinMemoryWindowHeight, kMaxMemoryWindowHeight);

            m_MemoryTypeColors[MemoryType.NativeHeap] = Color.red;
            m_MemoryTypeColors[MemoryType.JavaHeap] = Color.yellow;
            m_MemoryTypeColors[MemoryType.Code] = Color.blue;
            m_MemoryTypeColors[MemoryType.Stack] = Color.cyan;
            m_MemoryTypeColors[MemoryType.Graphics] = Color.green;
            m_MemoryTypeColors[MemoryType.PrivateOther] = Color.grey;
            m_MemoryTypeColors[MemoryType.System] = Color.magenta;
            m_MemoryTypeColors[MemoryType.AnonymousRSS] = Color.red;
            m_MemoryTypeColors[MemoryType.Swap] = new Color(1.0f, 0.5f, 0.0f);
            m_MemoryTypeColors[MemoryType.Total] = Color.white;
            ClearEntries();
            ValidateSettings();
        }

        /// <summary>
        /// Validate serialized settings here
        /// </summary>
        private void ValidateSettings()
        {
            var allMemoryTypes = (MemoryType[])Enum.GetValues(typeof(MemoryType));

            if (m_State.MemoryTypeEnabled == null || m_State.MemoryTypeEnabled.Length != allMemoryTypes.Length)
            {
                m_State.MemoryTypeEnabled = new bool[allMemoryTypes.Length];
                for (int i = 0; i < m_State.MemoryTypeEnabled.Length; i++)
                    m_State.MemoryTypeEnabled[i] = true;
            }

            m_State.MemoryWindowWidth = Mathf.Clamp(m_State.MemoryWindowWidth, kMinMemoryWindowWidth, kMaxMemoryWindowWidth);
        }

        internal void ClearEntries()
        {
            m_MeminfoHistory.Clear();
            m_ProcStatusHistory.Clear();

            m_UpperMemoryBoundry = 32 * AndroidMemoryStatistics.kOneMegaByte;
            m_ExpectedProcessFromRequest = null;
            m_ExpectedDevice = null;
        }

        internal void SetExpectedDeviceAndProcess(IAndroidLogcatDevice device, ProcessInformation process)
        {
            m_ExpectedDevice = device;
            m_ExpectedProcessFromRequest = process;
        }

        internal void QueueMemoryRequest(IAndroidLogcatDevice device, ProcessInformation process)
        {
            m_ExpectedDevice = device;
            m_ExpectedProcessFromRequest = process;
            if (m_ExpectedProcessFromRequest == null || !m_ExpectedProcessFromRequest.IsAlive() || m_ExpectedDevice == null)
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
                    adb = AndroidBridge.ADB.GetInstance(),
                    packageProcessId = m_ExpectedProcessFromRequest.processId,
                    packageName = m_ExpectedProcessFromRequest.name,
                    deviceId = device.Id,
                    // Anonymous RSS + Swap only needs /proc/<pid>/status. Skip dumpsys meminfo for it, since it's
                    // much heavier and triggers a garbage collection in the application, which affects the measurement.
                    queryMeminfo = m_State.MemoryGroup != MemoryGroup.AnonymousRSSAndSwap
                },
                QueryMemoryAsync,
                IntegrateQueryMemory,
                false);
        }

        internal static string UInt64ToSizeString(UInt64 value)
        {
            if (value < 0)
                return "unknown";
            if (value == 0)
                return "0 Bytes";
            float val = (float)value;
            string[] scale = new string[] { "TB", "GB", "MB", "KB", "Bytes" };
            int idx = scale.Length - 1;
            while (val >= AndroidMemoryStatistics.kOneKiloByte && idx >= 0)
            {
                val /= AndroidMemoryStatistics.kOneKiloByte;
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

            string outputMsg = string.Empty;
            if (workInput.queryMeminfo)
            {
                // Note: Using package name sometimes fails to dump memory, that's why we use process id
                //       Also using process id you can query memory from system apps which are not packages.
                var cmd = "-s " + workInput.deviceId + " shell dumpsys meminfo " + workInput.packageProcessId;
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

                try
                {
                    outputMsg = adb.Run(new[] { cmd }, $"Failed to query memory for {workInput.packageName} (PID = {workInput.packageProcessId}");
                }
                catch (Exception ex)
                {
                    AndroidLogcatInternalLog.Log("Failed to query memory: \n" + ex.Message);
                }
            }

            // RssAnon and VmSwap from /proc/<pid>/status make up Anonymous RSS + Swap, the memory usage metric in Google Play's Android vitals.
            // When the process has exited, the device shell prints the same message as dumpsys meminfo, so the exit is detected without dumpsys.
            string procStatus = string.Empty;
            var statusCmd = "-s " + workInput.deviceId + " shell cat /proc/" + workInput.packageProcessId + "/status 2>/dev/null || echo " + kProcessNotFound + " " + workInput.packageProcessId;
            AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), statusCmd);
            try
            {
                procStatus = adb.Run(new[] { statusCmd }, $"Failed to query process status for {workInput.packageName} (PID = {workInput.packageProcessId})");
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Failed to query process status: \n" + ex.Message);
            }

            var result = new AndroidLogcatQueryMemoryResult();
            result.deviceId = workInput.deviceId;
            result.packageName = workInput.packageName;
            result.packageProcessId = workInput.packageProcessId;
            result.contents = outputMsg;
            result.procStatusContents = procStatus;
            //AndroidLogcatInternalLog.Log(outputMsg);

            return result;
        }

        private void UpdateGeneralStats()
        {
            // Set the upper boundry depending on total memory of the selected group
            var history = ActiveHistory;
            UInt64 maxMemory = 0;
            for (int i = 0; i < history.Count; i++)
            {
                UInt64 localTotal = 0;
                foreach (var t in GetOrderMemoryTypes())
                {
                    if (!m_State.MemoryTypeEnabled[(int)t])
                        continue;
                    localTotal += history[i].GetValue(m_State.MemoryGroup, t);
                }
                maxMemory = Math.Max(maxMemory, localTotal);
            }
            // Keep boundry by 10% higher, so there would be visible room from the top of the window
            m_UpperMemoryBoundry = (UInt64)(1.1f * maxMemory);
        }

        private void InjectFakeMemoryStatistics(UInt64 totalMemory)
        {
            var stats = m_MeminfoHistory.Allocate();
            stats.SetPSSFakeData(totalMemory, totalMemory);
            stats.SetHeapAllocData(totalMemory, totalMemory);
            stats.SetHeapSizeData(totalMemory, totalMemory);
            m_ProcStatusHistory.Allocate().SetAnonymousRSSAndSwapData(totalMemory, totalMemory / 10);
            UpdateGeneralStats();
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
            if (m_ExpectedProcessFromRequest == null || m_ExpectedDevice == null)
                return;

            if (memoryResult.packageProcessId != m_ExpectedProcessFromRequest.processId ||
                memoryResult.deviceId != m_ExpectedDevice.Id)
                return;

            // Both are empty when the queries failed, contents is also empty when dumpsys meminfo wasn't run
            var hasMeminfo = !string.IsNullOrEmpty(memoryResult.contents);
            var hasProcStatus = !string.IsNullOrEmpty(memoryResult.procStatusContents);
            if (!hasMeminfo && !hasProcStatus)
                return;

            if ((hasMeminfo && memoryResult.contents.Contains(kProcessNotFound)) ||
                (hasProcStatus && memoryResult.procStatusContents.Contains(kProcessNotFound)))
            {
                m_ExpectedProcessFromRequest.SetExited();
                m_Parent.Repaint();
                return;
            }

            m_LastError = string.Empty;
            if (hasMeminfo)
            {
                var stats = m_MeminfoHistory.Allocate();
                try
                {
                    stats.Parse(memoryResult.contents);
                }
                catch (Exception ex)
                {
                    m_LastError = ex.Message;
                    stats.Clear();
                    AndroidLogcatInternalLog.Log(ex.Message);
                }
            }

            if (hasProcStatus)
            {
                var stats = m_ProcStatusHistory.Allocate();
                try
                {
                    stats.ParseProcStatus(memoryResult.procStatusContents);
                }
                catch (Exception ex)
                {
                    m_LastError = ex.Message;
                    stats.Clear();
                    AndroidLogcatInternalLog.Log(ex.Message);
                }
            }
            UpdateGeneralStats();

            m_Parent.Repaint();
        }

        private float GetEntryWidth(Rect windowSize)
        {
            return windowSize.width / (kMaxEntries - 1);
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
            var memory = m_ExpectedProcessFromRequest == null ? "0" : UInt64ToSizeString(ActiveHistory.LastEntry.GetValue(m_State.MemoryGroup, type));
            var name = String.Format("{0} ({1})", type, memory);
            if (type == MemoryType.Total)
            {
                GUI.backgroundColor = Color.white;
                GUILayout.Toggle(true, name, AndroidLogcatStyles.kSeriesLabel);
            }
            else
            {
                var enabled = m_State.MemoryTypeEnabled[(int)type];
                GUI.backgroundColor = enabled ? GetMemoryColor(type) : Color.black;
                EditorGUI.BeginChangeCheck();
                m_State.MemoryTypeEnabled[(int)type] = GUILayout.Toggle(enabled, name, AndroidLogcatStyles.kSeriesLabel);
                if (EditorGUI.EndChangeCheck())
                    UpdateGeneralStats();
            }
            GUI.backgroundColor = oldColor;
            GUILayout.EndHorizontal();
        }

        internal void DoGUI(ExtraWindowState extraWindowState)
        {
            var splitterRectVertical = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));
            var splitterRectHorizontal = new Rect(m_State.MemoryWindowWidth, splitterRectVertical.y, 5, extraWindowState.Height);

            if (!m_VerticalSplitter.Dragging)
                m_HorizontalSplitter.DoGUI(splitterRectHorizontal, ref m_State.MemoryWindowWidth);
            if (!m_HorizontalSplitter.Dragging)
                m_VerticalSplitter.DoGUI(splitterRectVertical, ref extraWindowState.Height);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(m_State.MemoryWindowWidth), GUILayout.Height(extraWindowState.Height));

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Group:");
            EditorGUI.BeginChangeCheck();
            m_State.MemoryGroup = (MemoryGroup)EditorGUILayout.EnumPopup(m_State.MemoryGroup);
            if (EditorGUI.EndChangeCheck())
                UpdateGeneralStats();
            GUILayout.EndHorizontal();

            foreach (var m in GetOrderMemoryTypes())
            {
                DoMemoryToggle(m);
            }

            DoMemoryToggle(MemoryType.Total);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            m_State.AutoCapture = GUILayout.Toggle(m_State.AutoCapture, "Auto Capture");
            if (!m_State.AutoCapture)
            {
                if (GUILayout.Button("Capture", EditorStyles.miniButton))
                    QueueMemoryRequest(m_ExpectedDevice, m_ExpectedProcessFromRequest);

            }
            GUILayout.EndHorizontal();

            if (Unsupported.IsDeveloperMode())
                DoDebuggingGUI();

            GUILayout.EndVertical();
            var rc = GUILayoutUtility.GetLastRect();


            GUILayout.BeginVertical();
            // Note: GUILayoutUtility.GetRect must be called for Layout event always
            var size = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.internalLogStyle, GUILayout.Height(extraWindowState.Height));

            size.height -= 4;


            var history = ActiveHistory;
            if (history.Count > 0)
                DoEntriesGUI(size, history);

            DoGuidelinesGUI(size, m_UpperMemoryBoundry);

            if (history.Count > 0)
                DoSelectedStatsGUI(size, history);

            GUI.Box(new Rect(rc.x + 4, size.y, rc.width - 4, size.height + 1), GUIContent.none, EditorStyles.helpBox);
            GUI.Box(new Rect(size.x, size.y, size.width + 1, size.height + 1), GUIContent.none, EditorStyles.helpBox);

            if (m_ExpectedProcessFromRequest == null)
                EditorGUI.HelpBox(size, "Select a package", MessageType.Info);
            else if (!string.IsNullOrEmpty(m_LastError))
            {
                var trimmed = m_LastError;
                const int maxErrorLength = 100;
                if (trimmed.Length > maxErrorLength)
                    trimmed = trimmed.Substring(0, maxErrorLength) + "...";
                EditorGUI.HelpBox(size, trimmed, MessageType.Error);
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private UInt64 AggregateMemorySize(AndroidMemoryStatistics stats, MemoryType type)
        {
            UInt64 total = 0;
            MemoryType[] types = GetOrderMemoryTypes();
            for (int i = types.Length - 1; i >= 0; i--)
            {
                if (types[i] == type)
                    return total;
                if (!m_State.MemoryTypeEnabled[(int)types[i]])
                    continue;
                total += stats.GetValue(m_State.MemoryGroup, types[i]);
            }

            throw new Exception("Unhandled memory type: " + type);
        }

        private void DoGuidelinesGUI(Rect windowSize, UInt64 totalMemorySize)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            m_Material.SetPass(0);
            var percentages = new[] { 0.4f, 0.8f };
            GL.Begin(GL.LINES);
            foreach (var p in percentages)
            {
                float y = windowSize.y + windowSize.height * (1.0f - p);
                GL.Color(Color.gray);
                GL.Vertex3(windowSize.x, y, 0);
                GL.Vertex3(windowSize.x + windowSize.width, y, 0);
            }
            GL.End();

            if (m_ExpectedProcessFromRequest == null)
                return;

            foreach (var p in percentages)
            {
                float y = windowSize.y + windowSize.height * (1.0f - p);
                var title = UInt64ToSizeString((UInt64)(totalMemorySize * p));
                AndroidLogcatStyles.infoStyle.Draw(new Rect(windowSize.x, y, 100, 20), new GUIContent(title), 0);
            }
        }

        private void DoEntriesGUI(Rect windowSize, MemoryHistory history)
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
            var xOffset = windowSize.x + windowSize.width - (history.Count - 1) * width;

            foreach (var m in GetOrderMemoryTypes())
            {
                if (!m_State.MemoryTypeEnabled[(int)m])
                    continue;
                GL.Begin(GL.TRIANGLE_STRIP);
                GL.Color(GetMemoryColor(m));

                for (int i = 0; i < history.Count; i++)
                {
                    var entry = history[i];
                    var agr = AggregateMemorySize(entry, m);
                    var val = entry.GetValue(m_State.MemoryGroup, m);
                    var x = xOffset + i * width;
                    var y1 = b - multiplier * (val + agr);
                    var y2 = b - multiplier * agr;
                    GL.Vertex3(x, y1, 0);
                    GL.Vertex3(x, y2, 0);
                }
                GL.End();
            }
        }

        private void DoSelectedStatsGUI(Rect windowSize, MemoryHistory history)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && windowSize.Contains(e.mousePosition))
            {
                float wd = GetEntryWidth(windowSize);
                var selectedEntry = (int)((e.mousePosition.x - windowSize.x + wd * 0.5f) / wd);
                // Correct entry for cases where we don't have enough entries to fill the full array
                history.SelectedEntry = selectedEntry + history.Count - kMaxEntries;
                m_Parent.Repaint();
            }

            if (history.SelectedEntry < 0)
                return;
            var width = GetEntryWidth(windowSize);
            var x = windowSize.x + windowSize.width - (history.Count - 1) * width + history.SelectedEntry * width;
            var t = windowSize.y;
            var b = windowSize.height + windowSize.y;
            if (e.type == EventType.Repaint)
            {
                m_Material.SetPass(0);
                GL.Begin(GL.LINES);
                GL.Color(Color.white);
                GL.Vertex3(x, t, 0);
                GL.Vertex3(x, b, 0);
                GL.End();
            }

            var selected = history[history.SelectedEntry];
            var info = new StringBuilder();

            int enabledCount = 0;
            foreach (var m in GetOrderMemoryTypes())
            {
                if (!m_State.MemoryTypeEnabled[(int)m])
                    continue;
                info.AppendLine(m.ToString() + " : " + UInt64ToSizeString(selected.GetValue(m_State.MemoryGroup, m)));
                enabledCount++;
            }

            info.AppendLine("Total: " + UInt64ToSizeString(selected.GetValue(m_State.MemoryGroup, MemoryType.Total)));

            const float kInfoWidth = 150;
            var infoX = x + 5;
            if (infoX + kInfoWidth > windowSize.x + windowSize.width)
                infoX -= kInfoWidth + 10;
            var rc = new Rect(infoX, t + 10, kInfoWidth, 19 * enabledCount + 30);
            GUI.Box(rc, GUIContent.none, GUI.skin.window);
            GUI.Label(rc, info.ToString());
        }

        void DoDebuggingGUI()
        {
            GUILayout.Space(20);
            GUILayout.Label("Developer Options", EditorStyles.boldLabel);
            if (GUILayout.Button("Add 400MB", EditorStyles.miniButton))
                InjectFakeMemoryStatistics(400 * AndroidMemoryStatistics.kOneMegaByte);
            if (GUILayout.Button("Add 2GB", EditorStyles.miniButton))
                InjectFakeMemoryStatistics(2 * AndroidMemoryStatistics.kOneGigaByte);
            if (GUILayout.Button("Add 1000GB", EditorStyles.miniButton))
                InjectFakeMemoryStatistics(1000 * AndroidMemoryStatistics.kOneGigaByte);
        }
    }
}
