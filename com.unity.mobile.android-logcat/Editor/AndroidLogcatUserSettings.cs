using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static Unity.Android.Logcat.AndroidLogcatConsoleWindow;
using static Unity.Android.Logcat.AndroidLogcatUserSettings;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatUserSettings
    {
        [Serializable]
        internal class InputSettings
        {
            [SerializeField]
            internal bool ShiftModifier;
            [SerializeField]
            internal string SendText;
            [SerializeField]
            internal PosixSignal PosixKillSignal;
            [SerializeField]
            internal ProcessInformation TargetProcess;
        }

        [Serializable]
        internal class ScreenCaptureSettings
        {
            /// <summary>Width of the saved screenshot list, left of the splitter.</summary>
            [SerializeField]
            internal float ScreenshotListWidth;
            [SerializeField]
            private string m_LastSaveLocation;

            /// <summary>
            /// Saves a copy of a capture wherever the user picks, starting from where
            /// they picked last time, and remembering where that was.
            /// </summary>
            internal void SaveFileAs(string path, string title)
            {
                var directory = AndroidLogcatUtilities.SaveFileAs(path, title, LastSaveLocation);
                if (directory != null)
                    LastSaveLocation = directory;
            }

            internal string LastSaveLocation
            {
                get
                {
                    if (string.IsNullOrEmpty(m_LastSaveLocation))
                        m_LastSaveLocation = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    return m_LastSaveLocation;
                }
                set { m_LastSaveLocation = value; }
            }
        }

        [Serializable]
        internal class QueryLayoutSettings
        {
            [SerializeField]
            internal string LastLayoutSaveLocation;
            [SerializeField]
            internal string LastScreenshotSaveLocation;
        }

        [SerializeField]
        private string m_SelectedDeviceId;
        [SerializeField]
        private ProcessInformation m_SelectedProcess;
        [SerializeField]
        private Priority m_SelectedPriority;
        private Dictionary<string, List<ProcessInformation>> m_KnownProcesses;
        [SerializeField]
        private List<ProcessInformation> m_KnownProcessesForSerialization;
        [SerializeField]
        private AndroidLogcatTags m_Tags;
        [SerializeField]
        private ExtraWindowState m_ExtraWindowState;
        [SerializeField]
        private AndroidLogcatMemoryViewerState m_MemoryViewerState;
        [SerializeField]
        private FilterOptions m_FilterOptions;
        [SerializeField]
        private List<ReordableListItem> m_SymbolPaths;
        [SerializeField]
        private ScreenCaptureSettings m_ScreenCaptureSettings;
        [SerializeField]
        private QueryLayoutSettings m_QueryLayoutSettings;
        [SerializeField]
        private InputSettings m_InputSettings;

        [SerializeField]
        private AutoScroll m_AutoScroll;

        public string LastSelectedDeviceId
        {
            set
            {
                m_SelectedDeviceId = value;
            }
            get
            {
                return m_SelectedDeviceId;
            }
        }

        public bool LastSelectedDeviceIdValid
        {
            get
            {
                return !string.IsNullOrEmpty(m_SelectedDeviceId);
            }
        }

        public ProcessInformation LastSelectedProcess
        {
            set
            {
                m_SelectedProcess = value;
            }
            get
            {
                return m_SelectedProcess;
            }
        }

        public bool SelectedPackageValid
        {
            get
            {
                return m_SelectedProcess != null &&
                    !string.IsNullOrEmpty(m_SelectedProcess.deviceId) &&
                    m_SelectedProcess.processId > 0;
            }
        }

        public Priority SelectedPriority
        {
            set
            {
                m_SelectedPriority = value;
            }
            get
            {
                return m_SelectedPriority;
            }
        }

        public ScreenCaptureSettings CaptureSettings { set => m_ScreenCaptureSettings = value; get => m_ScreenCaptureSettings; }
        public QueryLayoutSettings LayoutSettings { set => m_QueryLayoutSettings = value; get => m_QueryLayoutSettings; }
        public InputSettings DeviceInputSettings { set => m_InputSettings = value; get => m_InputSettings; }

        public AutoScroll AutoScroll { set => m_AutoScroll = value; get => m_AutoScroll; }

        private void RefreshProcessesForSerialization()
        {
            m_KnownProcessesForSerialization = new List<ProcessInformation>();
            foreach (var p in m_KnownProcesses)
            {
                m_KnownProcessesForSerialization.AddRange(p.Value);
            }
        }

        public IReadOnlyList<ProcessInformation> GetKnownProcesses(IAndroidLogcatDevice device)
        {
            return GetOrCreateProcessForDevice(device);
        }

        private List<ProcessInformation> GetOrCreateProcessForDevice(IAndroidLogcatDevice device)
        {
            if (device == null)
                return new List<ProcessInformation>();

            List<ProcessInformation> processes = null;
            if (!m_KnownProcesses.TryGetValue(device.Id, out processes))
            {
                processes = new List<ProcessInformation>();
                m_KnownProcesses[device.Id] = processes;
            }
            return processes;
        }

        public void CleanupDeadProcessesForDevice(IAndroidLogcatDevice device, int maxExitedPackagesToShow)
        {
            if (device == null)
                return;

            List<ProcessInformation> processes = null;
            if (!m_KnownProcesses.TryGetValue(device.Id, out processes))
                return;

            int deadProcessCount = 0;

            for (int i = 0; i < processes.Count; i++)
            {
                if (processes[i].IsAlive() == false)
                    deadProcessCount++;
            }

            // Need to remove the package which were added first, since they are the oldest packages
            int deadProcessesToRemove = deadProcessCount - maxExitedPackagesToShow;
            if (deadProcessesToRemove <= 0)
                return;

            for (int i = 0; i < processes.Count && deadProcessesToRemove > 0;)
            {
                if (processes[i].IsAlive())
                {
                    i++;
                    continue;
                }

                deadProcessesToRemove--;
                processes.RemoveAt(i);
            }

            RefreshProcessesForSerialization();
        }

        public ProcessInformation CreateProcessInformation(string processName, int pid, IAndroidLogcatDevice device)
        {
            if (pid <= 0)
                return null;

            if (device == null)
            {
                Debug.LogError("Cannot create package information, since there's no Android device connected.");
                return null;
            }

            var processes = GetOrCreateProcessForDevice(device);
            ProcessInformation info = processes.FirstOrDefault(package => package.processId == pid);
            if (info != null)
                return info;

            var newProcess = new ProcessInformation()
            {
                name = processName,
                processId = pid,
                deviceId = device.Id
            };

            processes.Add(newProcess);
            RefreshProcessesForSerialization();
            return newProcess;
        }

        private static Dictionary<string, List<ProcessInformation>> ProcessesToDictionary(List<ProcessInformation> allPackages)
        {
            var dictionaryProcesses = new Dictionary<string, List<ProcessInformation>>();
            foreach (var p in allPackages)
            {
                List<ProcessInformation> processes;
                if (!dictionaryProcesses.TryGetValue(p.deviceId, out processes))
                {
                    processes = new List<ProcessInformation>();
                    dictionaryProcesses[p.deviceId] = processes;
                }
                processes.Add(p);
            }

            return dictionaryProcesses;
        }

        public AndroidLogcatTags Tags
        {
            set
            {
                m_Tags = value;
            }
            get
            {
                return m_Tags;
            }
        }

        public ExtraWindowState ExtraWindowState { set => m_ExtraWindowState = value; get => m_ExtraWindowState; }

        public AndroidLogcatMemoryViewerState MemoryViewerState
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

        public FilterOptions FilterOptions
        {
            set
            {
                m_FilterOptions = value;
            }
            get
            {
                return m_FilterOptions;
            }
        }
        public List<ReordableListItem> SymbolPaths
        {
            get => m_SymbolPaths;
        }

        internal AndroidLogcatUserSettings()
        {
            Reset();
        }

        internal void Reset()
        {
            m_SelectedDeviceId = string.Empty;
            m_SelectedPriority = Priority.Verbose;
            m_Tags = new AndroidLogcatTags();
            m_KnownProcesses = new Dictionary<string, List<ProcessInformation>>();
            m_ExtraWindowState = new ExtraWindowState();
            m_MemoryViewerState = new AndroidLogcatMemoryViewerState();
            m_SymbolPaths = new List<ReordableListItem>();
            m_FilterOptions = new FilterOptions();
            m_AutoScroll = AutoScroll.Auto;

            ResetScreenCaptureSettings();

            m_QueryLayoutSettings = new QueryLayoutSettings();

            m_InputSettings = new InputSettings()
            {
                SendText = string.Empty,
                TargetProcess = new ProcessInformation()
            };
        }

        internal void ResetScreenCaptureSettings()
        {
            m_ScreenCaptureSettings = new ScreenCaptureSettings
            {
                ScreenshotListWidth = AndroidLogcatScreenshotList.kDefaultWidth
            };
        }

        internal static AndroidLogcatUserSettings Load(string path)
        {
            if (!File.Exists(path))
                return null;

            var jsonString = File.ReadAllText(path);
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                var settings = new AndroidLogcatUserSettings();
                JsonUtility.FromJsonOverwrite(jsonString, settings);
                settings.m_KnownProcesses = ProcessesToDictionary(settings.m_KnownProcessesForSerialization);
                return settings;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Load Preferences from Json failed: " + ex.Message);
            }
            return null;
        }

        internal static void Save(AndroidLogcatUserSettings settings, string path, AndroidLogcatRuntimeBase runtime)
        {
            if (settings == null)
                throw new NullReferenceException(nameof(settings));

            var jsonString = JsonUtility.ToJson(settings, true);
            if (string.IsNullOrEmpty(jsonString))
                return;

            File.WriteAllText(path, jsonString);
        }
    }
}
