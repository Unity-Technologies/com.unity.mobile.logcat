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

                    if (m_AppSummary.TryGetValue(name, out dummy))
                    {
                        throw new Exception("Error parsing app summary, value " + name + " was already parsed");
                    }
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
            public static AndroidMemoryStatistics Parse(string contents)
            {
                var statistics = new AndroidMemoryStatistics();
                string[] sections = contents.Split(new string[] { "MEMINFO", "App Summary", "Objects", "SQL" }, StringSplitOptions.RemoveEmptyEntries);
                if (sections.Length != 5)
                    throw new Exception("Expected 5 sections when parsing memory statistics:\n" + contents);
                statistics.ParseAppSummary(sections[2]);
                return statistics;
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

        private IAndroidLogcatRuntime m_Runtime;
        private Material m_Material;
        private string m_PackageName;
        private Rect m_WindowSize;


        const int kMaxEntries = 1000;
        private int[] m_Entries = new int[1000];
        private int m_CurrentEntry = 0;
        private int m_MaxMemorySize = int.MinValue;
        private int m_MinMemorySize = int.MaxValue;

        public AndroidLogcatMemoryViewer(string packageName)
        {
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Material = (Material)EditorGUIUtility.LoadRequired("SceneView/HandleLines.mat");
            m_PackageName = packageName;
        }


        public void QueueMemoryRequest()
        {
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
            var memoryResult = (AndroidLogcatQueryMemoryResult)result;
            var statistics = AndroidMemoryStatistics.Parse(memoryResult.contents);
            //statistics.Total;
            var memory = statistics.Total;
            m_Entries[m_CurrentEntry++] = memory;// statistics.Total;
            if (m_CurrentEntry >= kMaxEntries)
                m_CurrentEntry = 0;
            if (memory > m_MaxMemorySize)
                m_MaxMemorySize = memory;
            if (memory < m_MinMemorySize)
                m_MinMemorySize = memory;

           // UnityEngine.Debug.Log(statistics.Total);
        }

        internal void DoGUI()
        {

            // TODO: handle case where m_MaxMemorySize equals min
            if (m_CurrentEntry == 0 || m_MinMemorySize > m_MaxMemorySize || m_MaxMemorySize == m_MinMemorySize)
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
            var width = m_WindowSize.width / kMaxEntries;
            var multiplier = m_WindowSize.height / (m_MaxMemorySize - m_MinMemorySize);
            var b = m_WindowSize.height + m_WindowSize.y;

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(Color.red);
            float y = 0.0f;
            float x = 0.0f;
            for (int i = 0; i < m_CurrentEntry; i++)
            {
                var val = m_Entries[i] - m_MinMemorySize;
                x = i * width;
                y = b - multiplier * val;
                GL.Vertex3(x, y, 0);
                GL.Vertex3(x, b, 0);
            }

            // Finalize strip
            GL.Vertex3(x + width, y, 0);
            GL.Vertex3(x + width, b, 0);
            GL.End();
        }
    }
}

#endif