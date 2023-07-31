using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Unity.Android.Logcat
{
    internal partial class AndroidLogcatConsoleWindow : EditorWindow, IHasCustomMenu
    {
        private GUIContent kAutoRunText = new GUIContent(L10n.Tr("Auto Run"), L10n.Tr("Automatically launch logcat window during build & run."));
        private GUIContent kReconnect = new GUIContent(L10n.Tr("Reconnect"), L10n.Tr("Restart logcat process."));
        private GUIContent kDisconnect = new GUIContent(L10n.Tr("Disconnect"), L10n.Tr("Stop logcat process."));
        private GUIContent kFilterOptions = new GUIContent(L10n.Tr("Filter Options"));
        private GUIContent kClearButtonText = new GUIContent(L10n.Tr("Clear"), L10n.Tr("Clears logcat by executing adb logcat -c."));

        private Rect m_IpWindowScreenRect;


        private IReadOnlyList<ProcessInformation> ProcessesForSelectedDevice
        {
            get { return m_Runtime.UserSettings.GetKnownProcesses(m_Runtime.DeviceQuery.SelectedDevice); }
        }

        private SearchField m_SearchField;

        private AndroidLogcatRuntimeBase m_Runtime;
        private AndroidLogcat m_Logcat;
        private AndroidLogcatStatusBar m_StatusBar;
        private DateTime m_TimeOfLastAutoConnectUpdate;
        private DateTime m_TimeOfLastAutoConnectStart;
        private const byte kSpace = 3;
        private const int kMillisecondsBetweenConsecutiveDeviceChecks = 1000;
        private const int kMillisecondsBetweenConsecutiveAutoConnectChecks = 1000;
        private const int kMillisecondsMaxAutoconnectTimeOut = 5000;

        private bool m_AutoSelectProcess;
        private bool m_FinishedAutoselectingProcess;
        private bool m_ApplySettings;

        private AndroidLogcatMemoryViewer m_MemoryViewer;
        private AndroidLogcatInputs m_Inputs;
        private DateTime m_TimeOfLastMemoryRequest;

        private static string kAutoShowLogcatDuringBuildRun = "AutoShowLogcatDuringBuildRun";

        private ProcessInformation SelectedProcess
        {
            set
            {
                m_Runtime.UserSettings.LastSelectedProcess = value;
            }
            get
            {
                return m_Runtime.UserSettings.LastSelectedProcess;
            }
        }

        public bool AutoSelectProcess
        {
            set
            {
                m_AutoSelectProcess = value;
                m_FinishedAutoselectingProcess = false;
                m_TimeOfLastAutoConnectStart = DateTime.Now;
                if (m_StatusBar != null && m_AutoSelectProcess)
                    m_StatusBar.Message = "Waiting for '" + PlayerSettings.applicationIdentifier + "'";
            }

            get
            {
                return m_AutoSelectProcess;
            }
        }

        private bool IsLogcatConnected => m_Logcat != null && m_Logcat.IsConnected;

        internal void OnEnable()
        {
            OnEnableInternal(AndroidLogcatManager.instance.Runtime);
        }

        protected void OnEnableInternal(AndroidLogcatRuntimeBase runtime)
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            AndroidLogcatInternalLog.Log("OnEnable");
            m_Runtime = runtime;

            if (m_SearchField == null)
                m_SearchField = new SearchField();

            m_Runtime.UserSettings.Tags.TagSelectionChanged += TagSelectionChanged;

            m_TimeOfLastAutoConnectStart = DateTime.Now;
            m_Runtime.Update += OnUpdate;

            m_FinishedAutoselectingProcess = false;
            AndroidLogcatInternalLog.Log("Package: {0}, Auto select: {1}", PlayerSettings.applicationIdentifier, AutoSelectProcess);

            m_StatusBar = new AndroidLogcatStatusBar();

            m_Runtime.Settings.OnSettingsChanged += OnSettingsChanged;

            m_MemoryViewer = new AndroidLogcatMemoryViewer(this, m_Runtime);
            m_Inputs = new AndroidLogcatInputs();

            // Can't apply settings here, apparently EditorStyles aren't initialized yet.
            m_ApplySettings = true;

            m_Runtime.DeviceQuery.Clear();
            m_Runtime.DeviceQuery.DeviceSelected += OnSelectedDevice;

            // Since Runtime.OnDisable can be called earlier than this window OnClose, we must ensure the order
            m_Runtime.Closing += OnDisable;
        }

        internal void OnDisable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime == null)
            {
                AndroidLogcatInternalLog.Log("Runtime was already destroyed.");
                return;
            }
            m_Runtime.UserSettings.Tags.TagSelectionChanged -= TagSelectionChanged;

            m_Runtime.Closing -= OnDisable;

            m_Runtime.DeviceQuery.DeviceSelected -= OnSelectedDevice;

            if (m_Runtime.Settings != null)
                m_Runtime.Settings.OnSettingsChanged -= OnSettingsChanged;

            StopLogCat();

            m_Runtime.Update -= OnUpdate;
            AndroidLogcatInternalLog.Log("OnDisable, Auto select: {0}", m_AutoSelectProcess);
            m_Runtime = null;
        }

        private void OnSettingsChanged(AndroidLogcatSettings settings)
        {
            m_ApplySettings = true;
        }

        private void RemoveTag(string tag)
        {
            if (!m_Runtime.UserSettings.Tags.Remove(tag))
                return;

            RestartLogCat();
        }

        private void AddTag(string tag)
        {
            if (!m_Runtime.UserSettings.Tags.Add(tag, true))
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
            var processes = m_Runtime.UserSettings.GetKnownProcesses(selectedDevice);
            foreach (var p in processes)
            {
                if (p.processId == processId)
                {
                    SelectProcess(p);
                    return;
                }
            }

            var processName = AndroidLogcatUtilities.GetProcessNameFromPid(m_Runtime.Tools.ADB, selectedDevice, processId);

            var process = m_Runtime.UserSettings.CreateProcessInformation(processName, processId, selectedDevice);

            SelectProcess(process);
        }

        private void OnUpdate()
        {
            var deviceQuery = m_Runtime.DeviceQuery;

            if (deviceQuery.FirstConnectedDevice == null)
                deviceQuery.UpdateConnectedDevicesList(false);

            if (deviceQuery.FirstConnectedDevice == null)
                return;

            if (m_AutoSelectProcess && !m_FinishedAutoselectingProcess)
            {
                // This is for AutoRun triggered by "Build And Run".
                if ((DateTime.Now - m_TimeOfLastAutoConnectUpdate).TotalMilliseconds < kMillisecondsBetweenConsecutiveAutoConnectChecks)
                    return;
                AndroidLogcatInternalLog.Log("Waiting for {0} launch, elapsed {1} seconds", PlayerSettings.applicationIdentifier, (DateTime.Now - m_TimeOfLastAutoConnectStart).Seconds);
                m_TimeOfLastAutoConnectUpdate = DateTime.Now;

                var firstDevice = deviceQuery.FirstConnectedDevice;
                ResetProcesses(firstDevice);

                int projectApplicationPid = GetPidFromPackageName(null, PlayerSettings.applicationIdentifier, firstDevice);
                var process = m_Runtime.UserSettings.CreateProcessInformation(PlayerSettings.applicationIdentifier, projectApplicationPid, firstDevice);
                if (process != null)
                {
                    AndroidLogcatInternalLog.Log("Auto selecting process {0}", PlayerSettings.applicationIdentifier);
                    // Note: Don't call SelectPackage as that will reset m_AutoselectPackage
                    SelectedProcess = process;
                    deviceQuery.SelectDevice(firstDevice, false);

                    RestartLogCat();
                    m_FinishedAutoselectingProcess = true;
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
                        m_FinishedAutoselectingProcess = true;
                    }
                }
            }
            else
            {
                if (deviceQuery.SelectedDevice == null)
                {
                    IAndroidLogcatDevice selectedDevice;
                    ProcessInformation selectedProcess;
                    GetDeviceAndProcessFromSavedState(out selectedDevice, out selectedProcess);
                    if (selectedDevice == null)
                        selectedDevice = deviceQuery.FirstConnectedDevice;
                    if (selectedDevice != null)
                    {
                        SelectedProcess = null;
                        if (selectedProcess == null)
                        {
                            deviceQuery.SelectDevice(selectedDevice);
                        }
                        else
                        {
                            // We don't want for SelectDevice to start logcat, since we're gonna select a process
                            // That's why we're not notifying the listeners
                            deviceQuery.SelectDevice(selectedDevice, false);
                            SelectProcess(selectedProcess);
                        }
                    }
                }
            }

            if (IsLogcatConnected &&
                m_Runtime.UserSettings.MemoryViewerState.AutoCapture &&
                m_Runtime.UserSettings.ExtraWindowState.Type == ExtraWindow.Memory)
            {
                if ((DateTime.Now - m_TimeOfLastMemoryRequest).TotalMilliseconds > m_Runtime.Settings.MemoryRequestIntervalMS)
                {
                    m_TimeOfLastMemoryRequest = DateTime.Now;
                    m_MemoryViewer.QueueMemoryRequest(deviceQuery.SelectedDevice, SelectedProcess);
                }
            }
        }

        private void GetDeviceAndProcessFromSavedState(out IAndroidLogcatDevice savedDevice, out ProcessInformation savedProcess)
        {
            savedDevice = null;
            savedProcess = null;

            var settings = m_Runtime.UserSettings;

            if (!settings.LastSelectedDeviceIdValid)
                return;

            var savedDeviceId = settings.LastSelectedDeviceId;
            savedDevice = m_Runtime.DeviceQuery.GetDevice(savedDeviceId);
            savedProcess = settings.LastSelectedProcess;
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
            UpdateStatusBar();
        }

        private void OnNewLogEntryAdded(IReadOnlyList<LogcatEntry> entries)
        {
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
            var contextMenu = (AndroidContextMenu<ToolsContextMenu>)userData;
            var item = contextMenu.GetItemAt(selected);
            if (item == null)
                return;

            switch (item.Item)
            {
                case ToolsContextMenu.ScreenCapture:
                    AndroidLogcatScreenCaptureWindow.ShowWindow();
                    break;
                case ToolsContextMenu.OpenTerminal:
                    AndroidLogcatUtilities.OpenTerminal(Path.GetDirectoryName(m_Runtime.Tools.ADB.GetADBPath()));
                    break;
                case ToolsContextMenu.StacktraceUtility:
                    AndroidLogcatStacktraceWindow.ShowStacktraceWindow();
                    break;
                case ToolsContextMenu.WindowMemory:
                    m_Runtime.UserSettings.ExtraWindowState.Type = ExtraWindow.Memory;
                    break;
                case ToolsContextMenu.WindowInputs:
                    m_Runtime.UserSettings.ExtraWindowState.Type = ExtraWindow.Inputs;
                    break;
                case ToolsContextMenu.WindowHidden:
                    m_Runtime.UserSettings.ExtraWindowState.Type = ExtraWindow.Hidden;
                    break;
            }
        }

        private void DoToolsGUI()
        {
            GUILayout.Label(new GUIContent("Tools"), AndroidLogcatStyles.toolbarPopupCenter);
            var rect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                var contextMenu = new AndroidContextMenu<ToolsContextMenu>();
                contextMenu.Add(ToolsContextMenu.ScreenCapture, "Screen Capture");
                contextMenu.Add(ToolsContextMenu.OpenTerminal, "Open Terminal");
                contextMenu.Add(ToolsContextMenu.StacktraceUtility, "Stacktrace Utility");
                var b = m_Runtime.UserSettings.ExtraWindowState.Type;
                contextMenu.Add(ToolsContextMenu.WindowMemory, "Window/Memory", b == ExtraWindow.Memory);
                contextMenu.Add(ToolsContextMenu.WindowInputs, "Window/Inputs", b == ExtraWindow.Inputs);
                contextMenu.Add(ToolsContextMenu.WindowHidden, "Window/Disabled", b == ExtraWindow.Hidden);
                contextMenu.Show(new Vector2(rect.x, rect.yMax), MenuToolsSelection);
            }
        }

        internal void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            if (m_ApplySettings)
            {
                AndroidLogcatUtilities.ApplySettings(m_Runtime, m_Logcat);
                Repaint();
                m_ApplySettings = false;
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            {
                ShowDuringBuildRun = GUILayout.Toggle(ShowDuringBuildRun, kAutoRunText, AndroidLogcatStyles.toolbarButton);

                HandleSelectedDeviceField();

                EditorGUI.BeginDisabledGroup(!m_StatusBar.Connected);
                HandleSelectedProcess();
                EditorGUI.EndDisabledGroup();

                HandleSearchField();
                HandleFilterOptions();

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

            var extraWindow = m_Runtime.UserSettings.ExtraWindowState;
            switch (extraWindow.Type)
            {
                case ExtraWindow.Memory:
                    m_MemoryViewer.DoGUI(extraWindow);
                    break;
                case ExtraWindow.Inputs:
                    if (m_Inputs.DoGUI(m_Runtime, extraWindow))
                        UpdateDebuggablePackages();
                    break;
            }

            EditorGUILayout.BeginHorizontal();
            if (m_StatusBar != null)
                m_StatusBar.DoGUI();
            DoScrollOptionsGUI();
            EditorGUILayout.EndHorizontal();

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


            if (GUILayout.Button("AutoSelect " + AutoSelectProcess.ToString(), AndroidLogcatStyles.toolbarButton))
            {
                AutoSelectProcess = true;
            }

            m_Logcat?.DoDebuggingGUI();

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

            SelectedProcess = null;
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

        private void SetProcess(ProcessInformation newProcess)
        {
            SelectedProcess = newProcess;
            m_MemoryViewer.ClearEntries();
            m_MemoryViewer.SetExpectedDeviceAndProcess(m_Runtime.DeviceQuery.SelectedDevice, SelectedProcess);
        }

        private void SelectProcess(ProcessInformation newProcess)
        {
            if ((SelectedProcess == null && newProcess == null) ||
                (newProcess != null && SelectedProcess != null && newProcess.name == SelectedProcess.name && newProcess.processId == SelectedProcess.processId))
                return;

            m_AutoSelectProcess = false;

            AndroidLogcatInternalLog.Log("Selecting process {0}", newProcess == null ? "<null>" : newProcess.DisplayName);

            SetProcess(newProcess);
            RestartLogCat();
        }

        private void ProcessSelection(object userData, string[] options, int selected)
        {
            var processes = (ProcessInformation[])userData;
            SelectProcess(processes[selected]);
        }

        private void ResetProcesses(IAndroidLogcatDevice device)
        {
            AndroidLogcatInternalLog.Log("Reset processes");
            SetProcess(null);
        }

        private void HandleSelectedProcess()
        {
            // We always keep track the list of following packages:
            // * No Filter
            // * Package defined from player settings
            // * Package which is from top activity on phone and if it's not the one from player settings
            var displayName = SelectedProcess != null && SelectedProcess.processId != 0 ? SelectedProcess.DisplayName : "No Filter";
            GUILayout.Label(new GUIContent(displayName, "Select pacakge name"), AndroidLogcatStyles.toolbarPopup);
            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (m_Runtime.DeviceQuery.SelectedDevice == null)
                    return;

                UpdateDebuggablePackages();

                var processes = new List<ProcessInformation>(ProcessesForSelectedDevice);

                var appName = PlayerSettings.applicationIdentifier;
                processes.Sort(delegate (ProcessInformation x, ProcessInformation y)
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
                processes.Insert(0, null);

                var names = new GUIContent[processes.Count];
                int selectedProcessId = SelectedProcess == null || SelectedProcess.processId == 0 ? 0 : -1;
                for (int i = 0; i < processes.Count; i++)
                {
                    // Note: Some processes are named like /system/bin/something, this creates problems with Unity GUI, since it treats / in special way
                    names[i] = new GUIContent(processes[i] == null ? "No Filter" : AndroidLogcatUtilities.FixSlashesForIMGUI(processes[i].DisplayName));

                    if (processes[i] != null && SelectedProcess != null && SelectedProcess.name == processes[i].name && SelectedProcess.processId == processes[i].processId)
                        selectedProcessId = i;
                }

                EditorUtility.DisplayCustomMenu(
                    new Rect(rect.x, rect.yMax, 0, 0),
                    names,
                    selectedProcessId,
                    ProcessSelection, processes.ToArray());
            }

            GUILayout.Space(kSpace);
        }

        private void HandleSearchField()
        {
            var filterValid = m_Logcat != null ? m_Logcat.FilterOptions.IsValid : true;
            var oldColor = GUI.color;
            if (!filterValid)
                GUI.color = Color.red;
            var newFilter = m_SearchField.OnToolbarGUI(m_Runtime.UserSettings.FilterOptions.Filter, null);
            if (!filterValid)
                GUI.color = oldColor;
            SetFilter(newFilter);
        }


        private void HandleFilterOptions()
        {
            GUILayout.Label(kFilterOptions, AndroidLogcatStyles.toolbarPopupCenter, GUILayout.ExpandWidth(false));
            var rect = GUILayoutUtility.GetLastRect();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                var filterOptions = m_Runtime.UserSettings.FilterOptions;
                var contextMenu = new AndroidContextMenu<FilterContextMenu>();
                contextMenu.Add(FilterContextMenu.UseRegularExpressions, "Use Regular Expressions", filterOptions.UseRegularExpressions);
                contextMenu.Add(FilterContextMenu.MatchCase, "Match Case", filterOptions.MatchCase);

                void SearchOptionsSelection(object userData, string[] options, int selected)
                {
                    var sender = (AndroidContextMenu<FilterContextMenu>)userData;
                    var item = sender.GetItemAt(selected);
                    if (item == null)
                        return;
                    switch (item.Item)
                    {
                        case FilterContextMenu.UseRegularExpressions:
                            filterOptions.UseRegularExpressions = !filterOptions.UseRegularExpressions;
                            if (m_Logcat != null)
                                m_Logcat.FilterOptions.UseRegularExpressions = filterOptions.UseRegularExpressions;
                            break;
                        case FilterContextMenu.MatchCase:
                            filterOptions.MatchCase = !filterOptions.MatchCase;
                            if (m_Logcat != null)
                                m_Logcat.FilterOptions.MatchCase = filterOptions.MatchCase;
                            break;
                    }
                }

                contextMenu.Show(new Vector2(rect.x, rect.yMax), SearchOptionsSelection);
            }

        }

        private void OnSelectedDevice(IAndroidLogcatDevice device)
        {
            if (device == null)
                return;

            ResetProcesses(device);
            UpdateDebuggablePackages();
            RestartLogCat();
        }

        private void SetSelectedPriority(Priority newPriority)
        {
            if (newPriority != m_Runtime.UserSettings.SelectedPriority)
            {
                m_Runtime.UserSettings.SelectedPriority = newPriority;
                RestartLogCat();
            }
        }

        private void SetFilter(string newFilter)
        {
            if (newFilter == m_Runtime.UserSettings.FilterOptions.Filter)
                return;

            m_Runtime.UserSettings.FilterOptions.Filter = string.IsNullOrEmpty(newFilter) ? string.Empty : newFilter;
            if (m_Logcat != null)
                m_Logcat.FilterOptions.Filter = m_Runtime.UserSettings.FilterOptions.Filter;
        }

        private void CheckIfProcessExited(Dictionary<string, int> cache)
        {
            foreach (var process in ProcessesForSelectedDevice)
            {
                if (process == null || process.processId <= 0)
                    continue;

                if (GetPidFromPackageName(cache, process.name, m_Runtime.DeviceQuery.SelectedDevice) != process.processId)
                {
                    process.SetExited();
                }
                else
                {
                    process.SetAlive();
                }
            }
        }

        private void UpdateDebuggablePackages()
        {
            // When running test Tools don't exist
            if (m_Runtime.Tools == null)
                return;
            var startTime = DateTime.Now;
            var packagePIDCache = new Dictionary<string, int>();
            CheckIfProcessExited(packagePIDCache);

            int topActivityPid = 0;
            string topActivityPackageName = string.Empty;
            bool checkProjectPackage = true;
            var selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
            if (AndroidLogcatUtilities.GetTopActivityInfo(m_Runtime.Tools.ADB, selectedDevice, ref topActivityPackageName, ref topActivityPid)
                && topActivityPid > 0)
            {
                m_Runtime.UserSettings.CreateProcessInformation(topActivityPackageName, topActivityPid, selectedDevice);

                checkProjectPackage = topActivityPackageName != PlayerSettings.applicationIdentifier;
            }

            if (checkProjectPackage)
            {
                int projectApplicationPid = GetPidFromPackageName(packagePIDCache, PlayerSettings.applicationIdentifier, selectedDevice);
                m_Runtime.UserSettings.CreateProcessInformation(PlayerSettings.applicationIdentifier, projectApplicationPid, selectedDevice);
            }

            m_Runtime.UserSettings.CleanupDeadProcessesForDevice(m_Runtime.DeviceQuery.SelectedDevice, m_Runtime.Settings.MaxExitedPackagesToShow);
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

            pid = AndroidLogcatUtilities.GetPidFromPackageName(m_Runtime.Tools.ADB, device, packageName);
            if (cache != null)
                cache[packageName] = pid;
            return pid;
        }

        private void RestartLogCat()
        {
            StopLogCat();

            StartLogcat();
        }

        private void StartLogcat()
        {
            var device = m_Runtime.DeviceQuery.SelectedDevice;
            if (device == null)
                return;
            if (m_Runtime.Tools == null)
                return;

            m_Logcat = new AndroidLogcat(
                m_Runtime,
                m_Runtime.Tools.ADB,
                device,
                SelectedProcess == null ? 0 : SelectedProcess.processId,
                m_Runtime.UserSettings.SelectedPriority,
                m_Runtime.UserSettings.FilterOptions,
                m_Runtime.UserSettings.Tags.GetSelectedTags());
            m_Logcat.FilteredLogEntriesAdded += OnNewLogEntryAdded;
            m_Logcat.Disconnected += OnLogcatDisconnected;
            m_Logcat.Connected += OnLogcatConnected;

            m_Logcat.Start();
        }

        private void StopLogCat()
        {
            if (m_Logcat != null)
                m_Logcat.Stop();

            UpdateStatusBar();
        }

        private void ClearLogCat()
        {
            if (m_Logcat == null)
            {
                return;
            }

            m_Logcat.Stop();
            m_Logcat.Clear();
            m_Logcat.Start();
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
            var filterOptions = m_Runtime.UserSettings.FilterOptions;
            var text = filterOptions.Filter;
            var regex = filterOptions.UseRegularExpressions ? "On" : "Off";
            var tags = m_Runtime.UserSettings.Tags.ToString();
            var message = $"Filtering with Priority '{m_Runtime.UserSettings.SelectedPriority}'";
            if (!string.IsNullOrEmpty(tags))
                message += $", Tags '{m_Runtime.UserSettings.Tags.ToString()}'";
            if (!string.IsNullOrEmpty(text))
                message += $", Text '{filterOptions.Filter}', Regex '{regex}' Match Case '{filterOptions.MatchCase}'. ";

            UpdateStatusBar(message);
        }

        public void UpdateStatusBar(string message)
        {
            if (m_StatusBar == null)
                return;

            m_StatusBar.Connected = IsLogcatConnected;
            m_StatusBar.Message = message;

            Repaint();
        }

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
            wnd.AutoSelectProcess = autoSelectPackage;
            wnd.Show();
            wnd.Focus();

            return wnd;
        }
    }
}
