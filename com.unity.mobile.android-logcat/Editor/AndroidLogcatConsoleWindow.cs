using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
#if PLATFORM_ANDROID
using UnityEditor.Android;
#endif

namespace Unity.Android.Logcat
{
    internal partial class AndroidLogcatConsoleWindow : EditorWindow
#if PLATFORM_ANDROID
        , IHasCustomMenu
    {
        private GUIContent kAutoRunText = new GUIContent(L10n.Tr("Auto Run"), L10n.Tr("Automatically launch logcat window during build & run."));
        private GUIContent kReconnect = new GUIContent(L10n.Tr("Reconnect"), L10n.Tr("Restart logcat process."));
        private GUIContent kDisconnect = new GUIContent(L10n.Tr("Disconnect"), L10n.Tr("Stop logcat process."));
        private GUIContent kRegexText = new GUIContent(L10n.Tr("Regex"), L10n.Tr("Treat contents in search field as regex expression."));
        private GUIContent kClearButtonText = new GUIContent(L10n.Tr("Clear"), L10n.Tr("Clears logcat by executing adb logcat -c."));
        private const string kJsonFileEditorPrefKey = "AndroidLogcatStateJsonFile";
        private readonly string kAndroidLogcatSettingsPath = Path.Combine("ProjectSettings", "AndroidLogcatSettings.asset");

        private Rect m_IpWindowScreenRect;

        [Serializable]
        internal class PackageInformation
        {
            public string deviceId;
            public string name;
            public int processId;
            public bool exited;

            public string DisplayName
            {
                get
                {
                    var result = name + " (" + processId + ")";
                    if (exited)
                        result += " [Exited]";
                    return result;
                }
            }

            public PackageInformation()
            {
                Reset();
            }

            public void Reset()
            {
                deviceId = string.Empty;
                name = string.Empty;
                processId = 0;
                exited = false;
            }

            public void SetExited()
            {
                exited = true;
            }

            public void SetAlive()
            {
                exited = false;
            }

            public bool IsAlive()
            {
                return !exited && processId != 0;
            }
        }

        private PackageInformation m_SelectedPackage = null;

        private List<PackageInformation> PackagesForSelectedDevice
        {
            get { return GetPackagesForDevice(m_Runtime.DeviceQuery.SelectedDevice); }
        }

        private List<PackageInformation> GetPackagesForDevice(IAndroidLogcatDevice device)
        {
            if (device == null)
                return null;
            return m_PackagesForAllDevices[device.Id];
        }

        private Dictionary<string, List<PackageInformation>> m_PackagesForAllDevices = new Dictionary<string, List<PackageInformation>>();

        private AndroidLogcat.Priority m_SelectedPriority;

        private string m_Filter = string.Empty;
        private bool m_FilterIsRegularExpression;

        private SearchField m_SearchField;

        private AndroidLogcatTagsControl m_TagControl = null;

        private AndroidLogcatJsonSerialization m_JsonSerialization = null;

        private IAndroidLogcatRuntime m_Runtime;
        private AndroidLogcat m_LogCat;
        private AndroidLogcatStatusBar m_StatusBar;
        private ADB m_Adb;
        private DateTime m_TimeOfLastAutoConnectUpdate;
        private DateTime m_TimeOfLastAutoConnectStart;

        private List<AndroidLogcat.LogEntry> m_LogEntries = new List<AndroidLogcat.LogEntry>();

        private const byte kSpace = 3;
        private const int kMillisecondsBetweenConsecutiveDeviceChecks = 1000;
        private const int kMillisecondsBetweenConsecutiveAutoConnectChecks = 1000;
        private const int kMillisecondsMaxAutoconnectTimeOut = 5000;

        private bool m_AutoSelectPackage;
        private bool m_FinishedAutoselectingPackage;
        private bool m_ApplySettings;

        private AndroidLogcatMemoryViewer m_MemoryViewer;
        private DateTime m_TimeOfLastMemoryRequest;

        private static string kAutoShowLogcatDuringBuildRun = "AutoShowLogcatDuringBuildRun";

        public bool AutoSelectPackage
        {
            set
            {
                m_AutoSelectPackage = value;
                m_FinishedAutoselectingPackage = false;
                m_TimeOfLastAutoConnectStart = DateTime.Now;
                if (m_StatusBar != null && m_AutoSelectPackage)
                    m_StatusBar.Message = "Waiting for '" + PlayerSettings.applicationIdentifier + "'";
            }

            get
            {
                return m_AutoSelectPackage;
            }
        }

        internal void SaveStates()
        {
            var selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
            m_JsonSerialization = new AndroidLogcatJsonSerialization();
            m_JsonSerialization.m_SelectedDeviceId = selectedDevice != null ? selectedDevice.Id : "";
            m_JsonSerialization.m_SelectedPackage = m_SelectedPackage;
            m_JsonSerialization.m_SelectedPriority = m_SelectedPriority;
            m_JsonSerialization.m_TagControl = m_TagControl;
            m_JsonSerialization.m_MemoryViewerJson = JsonUtility.ToJson(m_MemoryViewer);

            // Convert Dictionary to List for serialization.
            var packagesForSerialization = new List<PackageInformation>();
            foreach (var p in m_PackagesForAllDevices)
            {
                packagesForSerialization.AddRange(p.Value);
            }
            m_JsonSerialization.m_PackagesForSerialization = packagesForSerialization;

            var jsonString = JsonUtility.ToJson(m_JsonSerialization, true);
            m_JsonSerialization = null;
            if (string.IsNullOrEmpty(jsonString))
                return;

            File.WriteAllText(kAndroidLogcatSettingsPath, jsonString);
        }

        internal void LoadStates()
        {
            if (!File.Exists(kAndroidLogcatSettingsPath))
                return;

            var jsonString = File.ReadAllText(kAndroidLogcatSettingsPath);
            if (string.IsNullOrEmpty(jsonString))
                return;

            try
            {
                m_JsonSerialization = JsonUtility.FromJson<AndroidLogcatJsonSerialization>(jsonString);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Load Preferences from Json failed: " + ex.Message);
                m_JsonSerialization = null;
                return;
            }

            JsonUtility.FromJsonOverwrite(m_JsonSerialization.m_MemoryViewerJson, m_MemoryViewer);
            m_MemoryViewer.ValidateSettings();

            // We can only restore Priority, TagControl & PackageForSerialization here.
            // For selected device & package, we have to delay it when we first launch the window.
            m_SelectedPriority = m_JsonSerialization.m_SelectedPriority;
            m_TagControl.TagNames = m_JsonSerialization.m_TagControl.TagNames;
            m_TagControl.SelectedTags = m_JsonSerialization.m_TagControl.SelectedTags;

            m_PackagesForAllDevices = new Dictionary<string, List<PackageInformation>>();
            foreach (var p in m_JsonSerialization.m_PackagesForSerialization)
            {
                List<PackageInformation> packages;
                if (!m_PackagesForAllDevices.TryGetValue(p.deviceId, out packages))
                {
                    packages = new List<PackageInformation>();
                    m_PackagesForAllDevices[p.deviceId] = packages;
                }
                packages.Add(p);
            }
        }

        private void OnEnable()
        {
            AndroidLogcatInternalLog.Log("OnEnable");
            m_Runtime = AndroidLogcatManager.instance.Runtime;

            if (m_Columns == null || m_Columns.Length != Enum.GetValues(typeof(Column)).Length)
                m_Columns = GetColumns();

            if (m_SearchField == null)
                m_SearchField = new SearchField();

            if (m_TagControl == null)
                m_TagControl = new AndroidLogcatTagsControl();
            m_TagControl.TagSelectionChanged += TagSelectionChanged;

            m_TimeOfLastAutoConnectStart = DateTime.Now;
            m_Runtime.Update += OnUpdate;

            m_FinishedAutoselectingPackage = false;
            AndroidLogcatInternalLog.Log("Package: {0}, Auto select: {1}", PlayerSettings.applicationIdentifier, AutoSelectPackage);

            m_StatusBar = new AndroidLogcatStatusBar();

            m_Runtime.Settings.OnSettingsChanged += OnSettingsChanged;

            m_MemoryViewer = new AndroidLogcatMemoryViewer(this);

            // Can't apply settings here, apparently EditorStyles aren't initialized yet.
            m_ApplySettings = true;

            m_Runtime.DeviceQuery.Clear();
            m_Runtime.DeviceQuery.DeviceSelected += OnSelectedDevice;

            LoadStates();
        }

        private void OnDisable()
        {
            SaveStates();

            m_Runtime.DeviceQuery.DeviceSelected -= OnSelectedDevice;

            if (m_Runtime.Settings != null)
                m_Runtime.Settings.OnSettingsChanged -= OnSettingsChanged;

            StopLogCat();

            m_Runtime.Update -= OnUpdate;
            AndroidLogcatInternalLog.Log("OnDisable, Auto select: {0}", m_AutoSelectPackage);
        }

        private void OnSettingsChanged(AndroidLogcatSettings settings)
        {
            m_ApplySettings = true;
        }

        private void ApplySettings(AndroidLogcatSettings settings)
        {
            int fixedHeight = settings.MessageFontSize + 5;
            AndroidLogcatStyles.kLogEntryFontSize = settings.MessageFontSize;
            AndroidLogcatStyles.kLogEntryFixedHeight = fixedHeight;
            AndroidLogcatStyles.background.fixedHeight = fixedHeight;
            AndroidLogcatStyles.backgroundEven.fixedHeight = fixedHeight;
            AndroidLogcatStyles.backgroundOdd.fixedHeight = fixedHeight;
            AndroidLogcatStyles.priorityDefaultStyle.font = settings.MessageFont;
            AndroidLogcatStyles.priorityDefaultStyle.fontSize = settings.MessageFontSize;
            AndroidLogcatStyles.priorityDefaultStyle.fixedHeight = fixedHeight;
            foreach (var p in (AndroidLogcat.Priority[])Enum.GetValues(typeof(AndroidLogcat.Priority)))
            {
                AndroidLogcatStyles.priorityStyles[(int)p].normal.textColor = settings.GetMessageColor(p);
                AndroidLogcatStyles.priorityStyles[(int)p].font = settings.MessageFont;
                AndroidLogcatStyles.priorityStyles[(int)p].fontSize = settings.MessageFontSize;
                AndroidLogcatStyles.priorityStyles[(int)p].fixedHeight = fixedHeight;
            }
            Repaint();
        }

        private void RemoveTag(string tag)
        {
            if (!m_TagControl.Remove(tag))
                return;

            RestartLogCat();
        }

        private void AddTag(string tag)
        {
            if (!m_TagControl.Add(tag, true))
                return;

            RestartLogCat();
        }

        private void TagSelectionChanged()
        {
            RestartLogCat();
        }

        private void FilterByProcessId(int processId)
        {
            var selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
            var packages = m_PackagesForAllDevices[selectedDevice.Id];
            foreach (var p in packages)
            {
                if (p.processId == processId)
                {
                    SelectPackage(p);
                    return;
                }
            }

            var packageName = AndroidLogcatUtilities.GetPackageNameFromPid(m_Adb, selectedDevice, processId);

            var package = CreatePackageInformation(packageName, processId, selectedDevice);

            SelectPackage(package);
        }

        private void OnUpdate()
        {
            var deviceQuery = m_Runtime.DeviceQuery;

            if (deviceQuery.FirstConnectedDevice == null)
                deviceQuery.UpdateConnectedDevicesList(false);

            if (deviceQuery.FirstConnectedDevice == null)
                return;

            if (m_AutoSelectPackage && !m_FinishedAutoselectingPackage)
            {
                // This is for AutoRun triggered by "Build And Run".
                if ((DateTime.Now - m_TimeOfLastAutoConnectUpdate).TotalMilliseconds < kMillisecondsBetweenConsecutiveAutoConnectChecks)
                    return;
                AndroidLogcatInternalLog.Log("Waiting for {0} launch, elapsed {1} seconds", PlayerSettings.applicationIdentifier, (DateTime.Now - m_TimeOfLastAutoConnectStart).Seconds);
                m_TimeOfLastAutoConnectUpdate = DateTime.Now;

                var firstDevice = deviceQuery.FirstConnectedDevice;
                ResetPackages(firstDevice);

                int projectApplicationPid = GetPidFromPackageName(null, PlayerSettings.applicationIdentifier, firstDevice);
                var package = CreatePackageInformation(PlayerSettings.applicationIdentifier, projectApplicationPid, firstDevice);
                if (package != null)
                {
                    AndroidLogcatInternalLog.Log("Auto selecting package {0}", PlayerSettings.applicationIdentifier);
                    // Note: Don't call SelectPackage as that will reset m_AutoselectPackage
                    m_SelectedPackage = package;
                    deviceQuery.SelectDevice(firstDevice, false);

                    RestartLogCat();
                    m_FinishedAutoselectingPackage = true;
                    UpdateStatusBar();
                }
                else
                {
                    var timeoutMS = (DateTime.Now - m_TimeOfLastAutoConnectStart).TotalMilliseconds;
                    if (timeoutMS > kMillisecondsMaxAutoconnectTimeOut)
                    {
                        var msg = string.Format("Timeout {0} ms while waiting for '{1}' to launch.", timeoutMS, PlayerSettings.applicationIdentifier);
                        UpdateStatusBar(msg);
                        AndroidLogcatInternalLog.Log(msg);
                        m_FinishedAutoselectingPackage = true;
                    }
                }
            }
            else
            {
                if (deviceQuery.SelectedDevice == null)
                {
                    IAndroidLogcatDevice selectedDevice;
                    PackageInformation selectedPackage;
                    GetDeviceAndPackageFromSavedState(out selectedDevice, out selectedPackage);
                    if (selectedDevice == null)
                        selectedDevice = deviceQuery.FirstConnectedDevice;
                    if (selectedDevice != null)
                    {
                        m_SelectedPackage = null;
                        if (selectedPackage == null)
                        {
                            deviceQuery.SelectDevice(selectedDevice);
                        }
                        else
                        {
                            // We don't want for SelectDevice to start logcat, since we're gonna select a package
                            // That's why we're not notifying the listeners
                            deviceQuery.SelectDevice(selectedDevice, false);
                            SelectPackage(selectedPackage);
                        }
                    }
                }
            }

            if (m_LogCat != null && m_LogCat.IsConnected && m_MemoryViewer.State == MemoryViewerState.Auto)
            {
                if ((DateTime.Now - m_TimeOfLastMemoryRequest).TotalMilliseconds > m_Runtime.Settings.MemoryRequestIntervalMS)
                {
                    m_TimeOfLastMemoryRequest = DateTime.Now;
                    m_MemoryViewer.QueueMemoryRequest(deviceQuery.SelectedDevice, m_SelectedPackage);
                }
            }
        }

        private void GetDeviceAndPackageFromSavedState(out IAndroidLogcatDevice savedDevice, out PackageInformation savedPackage)
        {
            savedDevice = null;
            savedPackage = null;

            if (m_JsonSerialization == null)
                return;

            var savedDeviceId = m_JsonSerialization.m_SelectedDeviceId;
            savedPackage = m_JsonSerialization.m_SelectedPackage;

            m_JsonSerialization = null;

            if (savedDeviceId == null)
                return;

            savedDevice = m_Runtime.DeviceQuery.GetDevice(savedDeviceId);
            if (savedDevice == null)
                return;
        }

        private void OnLogcatDisconnected(IAndroidLogcatDevice device)
        {
            StopLogCat();
            var msg = "Either adb application crashed or device disconnected (device id: " + device.DisplayName + ")";
            AndroidLogcatInternalLog.Log(msg);

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
            UpdateStatusBar(msg);
        }

        private void OnLogcatConnected(IAndroidLogcatDevice device)
        {
            UpdateStatusBar(string.Empty);
        }

        private void RemoveMessages(int count)
        {
            m_LogEntries.RemoveRange(0, count);

            // Modify selection indices
            for (int i = 0; i < m_SelectedIndices.Count; i++)
                m_SelectedIndices[i] -= count;

            // Remove selection indices which point to removed lines
            while (m_SelectedIndices.Count > 0 && m_SelectedIndices[0] < 0)
                m_SelectedIndices.RemoveAt(0);
        }

        private void OnNewLogEntryAdded(List<AndroidLogcat.LogEntry> entries)
        {
            m_LogEntries.AddRange(entries);
            if (m_LogEntries.Count > m_Runtime.Settings.MaxMessageCount)
            {
                RemoveMessages(m_LogEntries.Count - m_Runtime.Settings.MaxMessageCount);
            }
            Repaint();
        }

        internal static bool ShowDuringBuildRun
        {
            get
            {
                return EditorPrefs.GetBool(kAutoShowLogcatDuringBuildRun, true);
            }
            set
            {
                EditorPrefs.SetBool(kAutoShowLogcatDuringBuildRun, value);
            }
        }

        private void MenuToolsSelection(object userData, string[] options, int selected)
        {
            switch (selected)
            {
                case 0:
                    AndroidLogcatScreenCaptureWindow.ShowWindow();
                    break;
                case 1:
                    AndroidLogcatUtilities.OpenTerminal(Path.GetDirectoryName(GetCachedAdb().GetADBPath()));
                    break;
                case 2:
                    AndroidLogcatStacktraceWindow.ShowStacktraceWindow();
                    break;
                case 3:
                    m_MemoryViewer.State = MemoryViewerState.Auto;
                    break;
                case 4:
                    m_MemoryViewer.State = MemoryViewerState.Manual;
                    break;
                case 5:
                    m_MemoryViewer.State = MemoryViewerState.Hidden;
                    break;
            }
        }

        private void DoToolsGUI()
        {
            GUILayout.Label(new GUIContent("Tools"), AndroidLogcatStyles.toolbarPopupCenter);
            var rect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                var names = new[]
                {
                    "Screen Capture",
                    "Open Terminal",
                    "Stacktrace Utility",
                    "Memory Window/Auto Capture",
                    "Memory Window/Manual Capture",
                    "Memory Window/Disabled"
                }.Select(m => new GUIContent(m)).ToArray();

                int selected = -1;
                switch (m_MemoryViewer.State)
                {
                    case MemoryViewerState.Auto: selected = 3; break;
                    case MemoryViewerState.Manual: selected = 4; break;
                    case MemoryViewerState.Hidden: selected = 5; break;
                }

                EditorUtility.DisplayCustomMenu(new Rect(rect.x, rect.yMax, 0, 0), names, selected, MenuToolsSelection, null);
            }
        }

        internal void OnGUI()
        {
            if (m_ApplySettings)
            {
                ApplySettings(m_Runtime.Settings);
                m_ApplySettings = false;
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            {
                ShowDuringBuildRun = GUILayout.Toggle(ShowDuringBuildRun, kAutoRunText, AndroidLogcatStyles.toolbarButton);

                HandleSelectedDeviceField();

                EditorGUI.BeginDisabledGroup(!m_StatusBar.Connected);
                HandleSelectedPackage();

                HandleSearchField();

                SetRegex(GUILayout.Toggle(m_FilterIsRegularExpression, kRegexText, AndroidLogcatStyles.toolbarButton));

                EditorGUI.EndDisabledGroup();

                GUILayout.Space(kSpace);

                if (GUILayout.Button(kReconnect, AndroidLogcatStyles.toolbarButton))
                    RestartLogCat();
                if (GUILayout.Button(kDisconnect, AndroidLogcatStyles.toolbarButton))
                    StopLogCat();

                GUILayout.Space(kSpace);
                if (GUILayout.Button(kClearButtonText, AndroidLogcatStyles.toolbarButton))
                {
                    ClearLogCat();
                    Repaint();
                }

                DoToolsGUI();
            }
            EditorGUILayout.EndHorizontal();

            if (Unsupported.IsDeveloperMode())
                DoDebuggingGUI();

            if (DoMessageView())
            {
                Repaint();
            }

            m_MemoryViewer.DoGUI();

            if (m_StatusBar != null)
                m_StatusBar.DoGUI();

            EditorGUILayout.EndVertical();
        }

        private void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing debugging buttons:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            if (GUILayout.Button("Reload Me", AndroidLogcatStyles.toolbarButton))
            {
#if UNITY_2019_3_OR_NEWER
                EditorUtility.RequestScriptReload();
#else
                UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
#endif
            }


            if (GUILayout.Button("AutoSelect " + AutoSelectPackage.ToString(), AndroidLogcatStyles.toolbarButton))
            {
                AutoSelectPackage = true;
            }

            if (GUILayout.Button("Add Log lines", AndroidLogcatStyles.toolbarButton))
            {
                int count = 10000;
                var entries = new List<AndroidLogcat.LogEntry>(count);
                for (int i = 0; i < count; i++)
                    entries.Add(new AndroidLogcat.LogEntry() { processId = m_LogEntries.Count + i, message = "Dummy " + UnityEngine.Random.Range(0, int.MaxValue), tag = "sdsd" });
                OnNewLogEntryAdded(entries);
                Repaint();
            }

            if (GUILayout.Button("Remove Log Line", AndroidLogcatStyles.toolbarButton))
            {
                if (m_LogEntries.Count > 0)
                    RemoveMessages(1);
                Repaint();
            }

            // Have a sane number which represents that we cannot keep up with async items in queue
            // Usually this indicates a bug, since async operations starts being more and more delayed
            const int kMaxAsyncItemsInQueue = 100;
            var cannotKeepUp = m_Runtime.Dispatcher.AsyncOperationsInQueue > kMaxAsyncItemsInQueue;
            var style = cannotKeepUp ? AndroidLogcatStyles.errorStyle : AndroidLogcatStyles.infoStyle;
            var message = "Async Operation In Queue: " + m_Runtime.Dispatcher.AsyncOperationsInQueue + ", Executed: " + m_Runtime.Dispatcher.AsyncOperationsExecuted;
            if (cannotKeepUp)
                message += " (CAN'T KEEP UP!!!!)";
            GUILayout.Label(message, style);
            EditorGUILayout.EndHorizontal();
        }

        private void DeviceSelection(object userData, string[] options, int selected)
        {
            var devices = m_Runtime.DeviceQuery.Devices;
            if (selected >= m_Runtime.DeviceQuery.Devices.Count)
            {
                AndroidLogcatIPWindow.Show(this.m_Runtime, m_IpWindowScreenRect);
                return;
            }

            m_SelectedPackage = null;
            m_Runtime.DeviceQuery.SelectDevice(devices.Values.ToArray()[selected]);
        }

        private void HandleSelectedDeviceField()
        {
            var selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
            var currentSelectedDevice = selectedDevice == null ? "No device" : selectedDevice.DisplayName;
            GUILayout.Label(new GUIContent(currentSelectedDevice, "Select android device"), AndroidLogcatStyles.toolbarPopup);
            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // Only update device list, when we select this UI item
                m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);

                var names = m_Runtime.DeviceQuery.Devices.Select(m => new GUIContent(m.Value.ShortDisplayName)).ToList();
                names.Add(GUIContent.none);
                // Add <Enter IP> as last field to let user connect through wifi.
                names.Add(new GUIContent("Other connection options..."));

                // Store the screen-space place that we should show the AndroidLogcatIPWindow.
                m_IpWindowScreenRect = GUIUtility.GUIToScreenRect(rect);

                int selectedIndex = -1;
                selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
                for (int i = 0; i < names.Count && selectedDevice != null; i++)
                {
                    if (selectedDevice.Id == names[i].text)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                EditorUtility.DisplayCustomMenu(new Rect(rect.x, rect.yMax, 0, 0), names.ToArray(), CheckDeviceEnabled, selectedIndex, DeviceSelection, null);
            }

            GUILayout.Space(kSpace);
        }

        private bool CheckDeviceEnabled(int index)
        {
            // Enable items like <Enter IP>
            var devices = m_Runtime.DeviceQuery.Devices;
            if (index >= devices.Count)
                return true;
            return devices.Values.ToArray()[index].State == IAndroidLogcatDevice.DeviceState.Connected;
        }

        private void SetPacakge(PackageInformation newPackage)
        {
            m_SelectedPackage = newPackage;
            m_MemoryViewer.ClearEntries();
            m_MemoryViewer.SetExpectedDeviceAndPackage(m_Runtime.DeviceQuery.SelectedDevice, m_SelectedPackage);
        }

        private void SelectPackage(PackageInformation newPackage)
        {
            if ((m_SelectedPackage == null && newPackage == null) ||
                (newPackage != null && m_SelectedPackage != null && newPackage.name == m_SelectedPackage.name && newPackage.processId == m_SelectedPackage.processId))
                return;

            m_AutoSelectPackage = false;

            AndroidLogcatInternalLog.Log("Selecting pacakge {0}", newPackage == null ? "<null>" : newPackage.DisplayName);

            SetPacakge(newPackage);
            RestartLogCat();
        }

        private void PackageSelection(object userData, string[] options, int selected)
        {
            PackageInformation[] packages = (PackageInformation[])userData;
            SelectPackage(packages[selected]);
        }

        /// <summary>
        /// Removes dead packages from the list. Otherwise the list will grow forever
        /// </summary>
        /// <param name="packages"></param>
        private void CleanupDeadPackages()
        {
            List<PackageInformation> packages = PackagesForSelectedDevice;
            const int kMaxExitedPackages = 5;
            int deadPackageCount = 0;

            for (int i = 0; i < packages.Count; i++)
            {
                if (packages[i].IsAlive() == false)
                    deadPackageCount++;
            }

            // Need to remove the package which were added first, since they are the oldest packages
            int deadPackagesToRemove = deadPackageCount - kMaxExitedPackages;
            for (int i = 0; i < packages.Count && deadPackagesToRemove > 0;)
            {
                if (packages[i].IsAlive())
                {
                    i++;
                    continue;
                }

                deadPackagesToRemove--;
                packages.RemoveAt(i);
            }
        }

        private void ResetPackages(IAndroidLogcatDevice device)
        {
            AndroidLogcatInternalLog.Log("Reset packages");
            SetPacakge(null);
            if (device == null)
                return;
            List<PackageInformation> packages;
            if (!m_PackagesForAllDevices.TryGetValue(device.Id, out packages))
            {
                packages = new List<PackageInformation>();
                m_PackagesForAllDevices.Add(device.Id, packages);
            }
        }

        private void HandleSelectedPackage()
        {
            // We always keep track the list of following packages:
            // * No Filter
            // * Package defined from player settings
            // * Package which is from top activity on phone and if it's not the one from player settings
            var displayName = m_SelectedPackage != null && m_SelectedPackage.processId != 0 ? m_SelectedPackage.DisplayName : "No Filter";
            GUILayout.Label(new GUIContent(displayName, "Select package name"), AndroidLogcatStyles.toolbarPopup);
            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (m_Runtime.DeviceQuery.SelectedDevice == null)
                    return;

                UpdateDebuggablePackages();

                List<PackageInformation> packages = new List<PackageInformation>(PackagesForSelectedDevice);

                var appName = PlayerSettings.applicationIdentifier;
                packages.Sort(delegate(PackageInformation x, PackageInformation y)
                {
                    if (x.name == appName && !x.exited)
                        return -1;
                    if (y.name == appName && !y.exited)
                        return 1;
                    if (x.exited && !y.exited)
                        return 1;
                    if (!x.exited && y.exited)
                        return -1;
                    return 0;
                });

                // Add No Filter "package"
                packages.Insert(0, null);

                var names = new GUIContent[packages.Count];
                int selectedPackagedId = m_SelectedPackage == null || m_SelectedPackage.processId == 0 ? 0 : -1;
                for (int i = 0; i < packages.Count; i++)
                {
                    names[i] = new GUIContent(packages[i] == null ? "No Filter" : packages[i].DisplayName);

                    if (packages[i] != null && m_SelectedPackage != null && m_SelectedPackage.name == packages[i].name && m_SelectedPackage.processId == packages[i].processId)
                        selectedPackagedId = i;
                }

                EditorUtility.DisplayCustomMenu(
                    new Rect(rect.x, rect.yMax, 0, 0),
                    names,
                    selectedPackagedId,
                    PackageSelection, packages.ToArray());
            }

            GUILayout.Space(kSpace);
        }

        private void HandleSearchField()
        {
            var newFilter = m_SearchField.OnToolbarGUI(m_Filter, null);
            SetFilter(newFilter);
        }

        private void OnSelectedDevice(IAndroidLogcatDevice device)
        {
            if (device == null)
                return;

            ResetPackages(device);
            UpdateDebuggablePackages();
            RestartLogCat();
        }

        private void SetSelectedPriority(AndroidLogcat.Priority newPriority)
        {
            if (newPriority != m_SelectedPriority)
            {
                m_SelectedPriority = newPriority;
                RestartLogCat();
            }
        }

        private void SetFilter(string newFilter)
        {
            if (newFilter == m_Filter)
                return;

            m_Filter = string.IsNullOrEmpty(newFilter) ? string.Empty : newFilter;
            RestartLogCat();
        }

        private void SetRegex(bool newValue)
        {
            if (newValue == m_FilterIsRegularExpression)
                return;

            m_FilterIsRegularExpression = newValue;
            RestartLogCat();
        }

        private void ConnectToDevice(IAndroidLogcatDevice device)
        {
            if (device == null)
                return;

            var adb = GetCachedAdb();

            m_LogCat = new AndroidLogcat(
                m_Runtime,
                adb,
                device,
                m_SelectedPackage == null ? 0 : m_SelectedPackage.processId,
                m_SelectedPriority,
                m_Filter,
                m_FilterIsRegularExpression,
                m_TagControl.GetSelectedTags());
            m_LogCat.LogEntriesAdded += OnNewLogEntryAdded;
            m_LogCat.Disconnected += OnLogcatDisconnected;
            m_LogCat.Connected += OnLogcatConnected;

            m_LogCat.Start();
        }

        private void CheckIfPackagesExited(Dictionary<string, int> cache)
        {
            foreach (var package in PackagesForSelectedDevice)
            {
                if (package == null || package.processId <= 0)
                    continue;

                if (GetPidFromPackageName(cache, package.name, m_Runtime.DeviceQuery.SelectedDevice) != package.processId)
                {
                    package.SetExited();
                }
                else
                {
                    package.SetAlive();
                }
            }
        }

        private PackageInformation CreatePackageInformation(string packageName, int pid, IAndroidLogcatDevice device)
        {
            if (pid <= 0)
                return null;

            var packages = GetPackagesForDevice(device);
            PackageInformation info = packages.FirstOrDefault(package => package.processId == pid);
            if (info != null)
                return info;

            var newPackage = new PackageInformation()
            {
                name = packageName,
                processId = pid,
                deviceId = device.Id
            };

            packages.Add(newPackage);
            return newPackage;
        }

        private void UpdateDebuggablePackages()
        {
            var startTime = DateTime.Now;
            var packagePIDCache = new Dictionary<string, int>();
            CheckIfPackagesExited(packagePIDCache);

            int topActivityPid = 0;
            string topActivityPackageName = string.Empty;
            bool checkProjectPackage = true;
            var selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
            if (AndroidLogcatUtilities.GetTopActivityInfo(GetCachedAdb(), selectedDevice, ref topActivityPackageName, ref topActivityPid)
                && topActivityPid > 0)
            {
                CreatePackageInformation(topActivityPackageName, topActivityPid, selectedDevice);

                checkProjectPackage = topActivityPackageName != PlayerSettings.applicationIdentifier;
            }

            if (checkProjectPackage)
            {
                int projectApplicationPid = GetPidFromPackageName(packagePIDCache, PlayerSettings.applicationIdentifier, selectedDevice);
                CreatePackageInformation(PlayerSettings.applicationIdentifier, projectApplicationPid, selectedDevice);
            }

            CleanupDeadPackages();
            AndroidLogcatInternalLog.Log("UpdateDebuggablePackages finished in " + (DateTime.Now - startTime).Milliseconds + " ms");
        }

        private int GetPidFromPackageName(Dictionary<string, int> cache, string packageName, IAndroidLogcatDevice device)
        {
            if (device == null)
                return -1;
            // Getting pid for packages is a very costly operation, use cache to make less queries
            int pid;
            if (cache != null && cache.TryGetValue(packageName, out pid))
                return pid;

            var adb = GetCachedAdb();

            pid = AndroidLogcatUtilities.GetPidFromPackageName(adb, device, packageName);
            if (cache != null)
                cache[packageName] = pid;
            return pid;
        }

        private void RestartLogCat()
        {
            StopLogCat();

            m_LogEntries.Clear();

            ConnectToDevice(m_Runtime.DeviceQuery.SelectedDevice);
        }

        private void StopLogCat()
        {
            if (m_LogCat != null)
                m_LogCat.Stop();
            m_LogCat = null;
            UpdateStatusBar();
        }

        private void ClearLogCat()
        {
            if (m_LogCat == null)
            {
                m_LogEntries.Clear();
                m_SelectedIndices.Clear();
                return;
            }

            m_LogCat.Stop();
            m_LogEntries.Clear();
            m_SelectedIndices.Clear();
            m_LogCat.Clear();
            m_LogCat.Start();
        }

        private ADB GetCachedAdb()
        {
            if (m_Adb == null)
                m_Adb = ADB.GetInstance();

            return m_Adb;
        }

        public static void ShowInternalLog()
        {
            AndroidLogcatInternalLog.ShowLog(true);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(EditorGUIUtility.TrTextContent("Internal Log"), false, ShowInternalLog);
        }

        public void UpdateStatusBar()
        {
            UpdateStatusBar(string.Empty);
        }

        public void UpdateStatusBar(string message)
        {
            if (m_StatusBar == null)
                return;

            m_StatusBar.Connected = m_LogCat != null && m_LogCat.IsConnected;
            m_StatusBar.Message = message;

            Repaint();
        }

#else
    {
        internal void OnGUI()
        {
        #if !PLATFORM_ANDROID
            AndroidLogcatUtilities.ShowActivePlatformNotAndroidMessage();
        #endif
        }

#endif

        [MenuItem("Window/Analysis/Android Logcat &6")]
        internal static AndroidLogcatConsoleWindow ShowWindow()
        {
            return ShowNewOrExisting(false);
        }

        internal static AndroidLogcatConsoleWindow ShowNewOrExisting(bool autoSelectPackage)
        {
            var wnd = GetWindow<AndroidLogcatConsoleWindow>();
            if (wnd == null)
            {
                wnd = ScriptableObject.CreateInstance<AndroidLogcatConsoleWindow>();
            }

            wnd.titleContent = new GUIContent("Android Logcat");
#if PLATFORM_ANDROID
            wnd.AutoSelectPackage = autoSelectPackage;
#endif
            wnd.Show();
            wnd.Focus();

            return wnd;
        }
    }
}
