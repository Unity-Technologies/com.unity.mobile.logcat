using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal partial class AndroidLogcatConsoleWindow : EditorWindow, IHasCustomMenu, ISerializationCallbackReceiver
    {
        private int m_SelectedDeviceIndex;
        private string m_SelectedDeviceId;
        private string[] m_DeviceDetails = new string[0];
        private List<string> m_DeviceIds = new List<string>();
        private IDictionary<string, AndroidDevice> m_CachedDevices = new Dictionary<string, AndroidDevice>();
        private GUIContent kAutoRunText = new GUIContent(L10n.Tr("Auto Run"), L10n.Tr("Automatically launch logcat window during build & run."));
        private GUIContent kReconnect = new GUIContent(L10n.Tr("Reconnect"), L10n.Tr("Restart logcat process."));
        private GUIContent kRegexText = new GUIContent(L10n.Tr("Regex"), L10n.Tr("Treat contents in search field as regex expression."));
        private GUIContent kClearButtonText = new GUIContent(L10n.Tr("Clear"), L10n.Tr("Clears logcat by executing adb logcat -c."));
        private GUIContent kCaptureScreenText = new GUIContent(L10n.Tr("Capture Screen"), L10n.Tr("Capture the current screen on the device."));

        private Rect m_IPWindowScreenRect;

        private enum PackageType
        {
            None,
            DefinedFromPlayerSettings,
            TopActivityPackage
        }

        [Serializable]
        private class PackageInformation
        {
            public string deviceId;
            public string name;
            public string displayName;
            public int processId;
            public bool exited;

            public PackageInformation()
            {
                Reset();
            }

            public void Reset()
            {
                deviceId = string.Empty;
                name = string.Empty;
                displayName = string.Empty;
                processId = 0;
                exited = false;
            }
        }

        [SerializeField]
        private PackageInformation m_SelectedPackage = null;
    
        private List<PackageInformation> PackagesForSelectedDevice
        {
            get { return GetPackagesForDevice(m_SelectedDeviceId); }
        }

        private List<PackageInformation> GetPackagesForDevice(string deviceId)
        {
            return m_PackagesForAllDevices[deviceId];
        }

        private Dictionary<string, List<PackageInformation>> m_PackagesForAllDevices = new Dictionary<string, List<PackageInformation>>();

        [SerializeField]
        private List<PackageInformation> m_PackagesForSerialization = new List<PackageInformation> ();


        [SerializeField]
        private AndroidLogcat.Priority m_SelectedPriority;

        private string m_Filter = string.Empty;
        private bool m_FilterIsRegularExpression;

        [SerializeField]
        private AndroidLogcatTagsControl m_TagControl;

        private AndroidLogcat m_LogCat;
        private AndroidLogcatStatusBar m_StatusBar;
        private ADB m_Adb;
        private DateTime m_TimeOfLastDeviceListUpdate;
        private DateTime m_TimeOfLastAutoConnectUpdate;
        private DateTime m_TimeOfLastAutoConnectStart;

        private List<AndroidLogcat.LogEntry> m_LogEntries = new List<AndroidLogcat.LogEntry>();

        private const byte kSpace = 3;
        private const int kMillisecondsBetweenConsecutiveDeviceChecks = 1000;
        private const int kMillisecondsBetweenConsecutiveAutoConnectChecks = 1000;
        private const int kMillisecondsMaxAutoconnectTimeOut = 5000;

        private bool m_AutoSelectPackage;
        private bool m_FinishedAutoselectingPackage;

        private static string kAutoShowLogcatDuringBuildRun = "AutoShowLogcatDuringBuildRun";

        public bool AutoSelectPackage
        {
            set
            {
                m_AutoSelectPackage = value;
                m_FinishedAutoselectingPackage = false;
                if (m_StatusBar != null && m_AutoSelectPackage)
                    m_StatusBar.Message = "Waiting for '" + PlayerSettings.applicationIdentifier + "'";
            }

            get
            {
                return m_AutoSelectPackage;
            }
        }

        private void OnEnable()
        {
            AndroidLogcatInternalLog.Log("OnEnable");
            if (m_TagControl == null)
                RecreateTags();
            m_TagControl.TagSelectionChanged += TagSelectionChanged;

            m_SelectedDeviceIndex = -1;
            m_SelectedDeviceId = null;

            m_TimeOfLastAutoConnectStart = DateTime.Now;
            EditorApplication.update += Update;

            m_FinishedAutoselectingPackage = false;
            AndroidLogcatInternalLog.Log("Package: {0}, Auto select: {1}", PlayerSettings.applicationIdentifier, m_AutoSelectPackage);

            m_StatusBar = new AndroidLogcatStatusBar();
            if (m_AutoSelectPackage)
                m_StatusBar.Message = "Waiting for '" + PlayerSettings.applicationIdentifier + "'";
        }

        private void OnDisable()
        {
            StopLogCat();
            EditorApplication.update -= Update;
            AndroidLogcatInternalLog.Log("OnDisable, Auto select: {0}", m_AutoSelectPackage);
        }

        public void OnBeforeSerialize()
        {
            m_PackagesForSerialization.Clear();

            foreach (var p in m_PackagesForAllDevices)
            {
                m_PackagesForSerialization.AddRange(p.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            m_PackagesForAllDevices = new Dictionary<string, List<PackageInformation>>();

            foreach (var p in m_PackagesForSerialization)
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

        private void RemoveTag(string tag)
        {
            if (!m_TagControl.Remove(tag, true))
                return;

            RestartLogCat();
        }

        private void AddTag(string tag)
        {
            if (!m_TagControl.Add(tag, true))
                return;

            RestartLogCat();
        }

        private void RecreateTags()
        {
            m_TagControl = new AndroidLogcatTagsControl();

            m_TagControl.Add("Unity");
            m_TagControl.Add("CRASH");
        }

        private void TagSelectionChanged()
        {
            RestartLogCat();
        }

        private void ClearTags()
        {
            RecreateTags();
            RestartLogCat();
        }

        private void Update()
        {
            if (m_DeviceIds?.Count == 0)
                UpdateConnectedDevicesList(false);

            if (m_DeviceIds.Count == 0)
                return;

            if (m_AutoSelectPackage && !m_FinishedAutoselectingPackage)
            {
                if ((DateTime.Now - m_TimeOfLastAutoConnectUpdate).TotalMilliseconds < kMillisecondsBetweenConsecutiveAutoConnectChecks)
                    return;
                m_TimeOfLastAutoConnectUpdate = DateTime.Now;

                ResetPackages(m_DeviceIds[0]);

                int projectApplicationPid = GetPIDFromPackageName(PlayerSettings.applicationIdentifier, m_DeviceIds[0]);
                var package = CreatePackageInformation(PlayerSettings.applicationIdentifier, projectApplicationPid, m_DeviceIds[0]);
                if (package != null)
                {
                    // Note: Don't call SelectPackage as that will reset m_AutoselectPackage
                    m_SelectedPackage = package;
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
                if (m_SelectedDeviceId == null)
                {
                    SetSelectedDeviceByIndex(0, true);
                }
            }
        }

        private void OnDeviceDisconnected(string deviceId)
        {
            StopLogCat();
            var msg = $"Either adb.exe crashed or device disconnected (device id: {GetDeviceDetailsFor(deviceId)})";
            AndroidLogcatInternalLog.Log(msg);
            UpdateStatusBar(msg);
            var index = m_DeviceIds.IndexOf(deviceId);
            if (index == -1)
                return;

            m_DeviceIds.RemoveAt(index);
            ArrayUtility.RemoveAt(ref m_DeviceDetails, index);
        }

        private void OnDeviceConnected(string deviceId)
        {
            UpdateStatusBar(string.Empty);
        }

        private void OnNewLogEntryAdded(List<AndroidLogcat.LogEntry> entries)
        {
            m_LogEntries.AddRange(entries);
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
                wnd = ScriptableObject.CreateInstance<AndroidLogcatConsoleWindow>();
            wnd.titleContent = new GUIContent("Android Logcat");
            wnd.AutoSelectPackage = autoSelectPackage;
            wnd.Show();
            wnd.Focus();

            return wnd;
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

        internal void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            {
                ShowDuringBuildRun = GUILayout.Toggle(ShowDuringBuildRun, kAutoRunText, AndroidLogcatStyles.toolbarButton);

                HandleSelectedDeviceField();

                HandleSelectedPackage();

                HandleSearchField();
                SetRegex(GUILayout.Toggle(m_FilterIsRegularExpression, kRegexText, AndroidLogcatStyles.toolbarButton));

                GUILayout.Space(kSpace);

                if (GUILayout.Button(kReconnect, AndroidLogcatStyles.toolbarButton))
                    RestartLogCat();

                GUILayout.Space(kSpace);
                if (GUILayout.Button(kClearButtonText, AndroidLogcatStyles.toolbarButton))
                {
                    ClearLogCat();
                    Repaint();
                }

                GUILayout.Space(kSpace);
                if (GUILayout.Button(kCaptureScreenText, AndroidLogcatStyles.toolbarButton))
                {
                    CaptureScreen();
                    Repaint();
                }

                // Don't erase, used for debugging purposes
                /*
                if (GUILayout.Button("Reload Me", AndroidLogcatStyles.toolbarButton))
                {
                    UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
                }
               
                if (GUILayout.Button("AutoSelect " + m_AutoSelectPackage.ToString(), AndroidLogcatStyles.toolbarButton))
                {
                    m_AutoSelectPackage = true;
                }

                if (GUILayout.Button("Stop logcat ", AndroidLogcatStyles.toolbarButton))
                {
                    StopLogCat();
                }

                if (GUILayout.Button("Add Log lines", AndroidLogcatStyles.toolbarButton))
                {
                    for (int i = 0; i < 7000; i++)
                        m_LogEntries.Add(new AndroidLogcat.LogEntry() { processId = i, message = "Dummy", tag = "sdsd" });
                    Repaint();
                }

                if (GUILayout.Button("Remove All Log Lines", AndroidLogcatStyles.toolbarButton))
                {
                    m_LogEntries.RemoveAt(0);
                    Repaint();
                }
                // Debugging purposes */
            }
            EditorGUILayout.EndHorizontal();
            if (DoMessageView())
            {
                Repaint();
            }

            m_StatusBar?.DoGUI();

            EditorGUILayout.EndVertical();
        }

        private void DeviceSelection(object userData, string[] options, int selected)
        {
            if (selected == m_DeviceIds.Count)
            {
                AndroidLogcatIPWindow.Show(this, m_IPWindowScreenRect);
                return;
            }

            SetSelectedDeviceByIndex(selected);
        }

        public void ConnectDeviceByIPAddress(string ip)
        {
            var cmd = $"connect {ip}";
            var errorMsg = $"Unable to connect to {ip}.";
            var outputMsg = GetCachedAdb().Run(new[] { cmd }, errorMsg);
            if (outputMsg.StartsWith(errorMsg))
                Debug.LogError(outputMsg);
        }

        private void HandleSelectedDeviceField()
        {
            var currentSelectedDevice = m_SelectedDeviceIndex >= 0 && m_SelectedDeviceIndex < m_DeviceDetails.Length ? m_DeviceDetails[m_SelectedDeviceIndex] : "No device";
            GUILayout.Label(new GUIContent(currentSelectedDevice, "Select android device"), AndroidLogcatStyles.toolbarPopup);
            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // Only update device list, when we select this UI item
                UpdateConnectedDevicesList(true);

                var names = m_DeviceDetails.Select(m => new GUIContent(m)).ToList();
                // Add <Enter IP> as last field to let user connect through wifi.
                names.Add(new GUIContent("<Enter IP>"));

                // Store the screen-space place that we should show the AndroidLogcatIPWindow.
                m_IPWindowScreenRect = GUIUtility.GUIToScreenRect(rect);

                EditorUtility.DisplayCustomMenu(new Rect(rect.x, rect.yMax, 0, 0), names.ToArray(), CheckDeviceEnabled, m_SelectedDeviceIndex, DeviceSelection, null);
            }

            GUILayout.Space(kSpace);
        }

        private bool CheckDeviceEnabled(int index)
        {
            return true;
        }

        private void SelectPackage(PackageInformation newPackage)
        {
            if ((m_SelectedPackage == null && newPackage == null) ||
                (newPackage != null && m_SelectedPackage != null && newPackage.name == m_SelectedPackage.name && newPackage.processId == m_SelectedPackage.processId))
                return;

            m_AutoSelectPackage = false;
            m_SelectedPackage = newPackage;

            RestartLogCat();
        }

        private void PackageSelection(object userData, string[] options, int selected)
        {
            PackageInformation[] packages = (PackageInformation[])userData;
            SelectPackage(packages[selected]);
        }

        private void ResetPackages(string deviceId)
        {
            m_SelectedPackage = null;

            if (!m_PackagesForAllDevices.TryGetValue(deviceId, out List<PackageInformation> packages))
            {
                packages = new List<PackageInformation>();
                m_PackagesForAllDevices.Add(deviceId, packages);
            }
        }

        private void HandleSelectedPackage()
        {
            // We always keep track the list of following packages:
            // * No Filter
            // * Package defined from player settings
            // * Package which is from top activity on phone and if it's not the one from player settings
            var displayName = m_SelectedPackage != null && m_SelectedPackage.processId != 0 ? m_SelectedPackage.displayName : "No Filter";
            GUILayout.Label(new GUIContent(displayName, "Select package name"), AndroidLogcatStyles.toolbarPopup);
            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
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
                    names[i] = new GUIContent(packages[i] == null ? "No Filter" : packages[i].displayName);

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
            const string kSearchFieldControlName = "LogcatSearch";

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName(kSearchFieldControlName);
            var newFilter = EditorGUILayout.DelayedTextField(m_Filter, AndroidLogcatStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                SetFilter(newFilter);
                EditorGUI.FocusTextInControl(kSearchFieldControlName);
            }
        }

        private void SetSelectedDeviceByIndex(int newDeviceIndex, bool force = false)
        {
            if (newDeviceIndex != m_SelectedDeviceIndex || force)
            {
                m_SelectedDeviceIndex = newDeviceIndex;
                m_SelectedDeviceId = m_DeviceIds[m_SelectedDeviceIndex];
                ResetPackages(m_SelectedDeviceId);
                UpdateDebuggablePackages();
                RestartLogCat();
            }
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

        private void ConnectToDeviceId(string deviceId)
        {
            if (deviceId == null)
                return;

            var adb = GetCachedAdb();
            var device = GetAndroidDeviceFromCache(adb, deviceId);

            m_LogCat = new AndroidLogcat(adb, device, m_SelectedPackage == null ? 0 : m_SelectedPackage.processId, m_SelectedPriority, m_Filter, m_FilterIsRegularExpression, m_TagControl.GetSelectedTags());
            m_LogCat.LogEntriesAdded += OnNewLogEntryAdded;
            m_LogCat.DeviceDisconnected += OnDeviceDisconnected;
            m_LogCat.DeviceConnected += OnDeviceConnected;

            m_LogCat.Start();
        }

        private void UpdateConnectedDevicesList(bool immediate)
        {
            if ((DateTime.Now - m_TimeOfLastDeviceListUpdate).TotalMilliseconds < kMillisecondsBetweenConsecutiveDeviceChecks && !immediate)
                return;
            m_TimeOfLastDeviceListUpdate = DateTime.Now;

            var adb = GetCachedAdb();

            m_DeviceIds = RetrieveConnectDevicesIDs(adb);

            // Ensure selected device does not change (due to a new device name taking the same index)
            if (m_SelectedDeviceId != null)
            {
                m_SelectedDeviceIndex = m_DeviceIds.IndexOf(m_SelectedDeviceId);
            }

            var devicesDetails = new List<string>();
            foreach (var deviceId in m_DeviceIds)
            {
                devicesDetails.Add(RetrieveDeviceDetailsFor(adb, deviceId));
            }
            m_DeviceDetails = devicesDetails.ToArray();
        }

        private void CheckIfPackageExited(PackageInformation package)
        {
            if (package != null &&
                package.processId > 0 &&
                !package.exited &&
                GetPIDFromPackageName(package.name) != package.processId)
            {
                m_SelectedPackage.exited = true;
                m_SelectedPackage.displayName += " [Exited]";
            }
        }

        private PackageInformation CreatePackageInformation(string packageName, int pid, string deviceId)
        {
            if (pid <= 0)
                return null;

            var packages = GetPackagesForDevice(deviceId);
            PackageInformation info = packages.FirstOrDefault(package => package.processId == pid);
            if (info != null)
                return info;

            var newPackage = new PackageInformation()
            {
                name = packageName,
                displayName = $"{packageName} ({pid})",
                processId = pid,
                deviceId = deviceId
            };

            packages.Add(newPackage);
            return newPackage;
        }

        private void UpdateDebuggablePackages()
        {
            CheckIfPackageExited(m_SelectedPackage);

            int topActivityPid = 0;
            string topActivityPackageName = string.Empty;
            bool checkProjectPackage = true;
            if (GetCurrentPackage(ref topActivityPackageName, ref topActivityPid) && topActivityPid > 0)
            {
                CreatePackageInformation(topActivityPackageName, topActivityPid, m_SelectedDeviceId);

                checkProjectPackage = topActivityPackageName != PlayerSettings.applicationIdentifier;
            }

            if (checkProjectPackage)
            {
                int projectApplicationPid = GetPIDFromPackageName(PlayerSettings.applicationIdentifier);
                CreatePackageInformation(PlayerSettings.applicationIdentifier, projectApplicationPid, m_SelectedDeviceId);
            }
        }

        private int GetPIDFromPackageName(string packageName, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return -1;

            try
            {
                var adb = GetCachedAdb();
                var device = GetAndroidDeviceFromCache(adb, deviceId);
                var pidofOptionAvailable = Int32.Parse(device.Properties["ro.build.version.sdk"]) >= 24; // pidof option is only available in Android 7 or above.

                string cmd = null;
                if (pidofOptionAvailable)
                    cmd = $"-s {deviceId} shell pidof -s {packageName}";
                else
                    cmd = $"-s {deviceId} shell \"ps | grep {packageName}$\"";

                AndroidLogcatInternalLog.Log($"{adb.GetADBPath()} {cmd}");
                var output = adb.Run(new[] { cmd }, "Unable to get the pid of the given packages.");
                if (string.IsNullOrEmpty(output))
                    return -1;

                AndroidLogcatInternalLog.Log(output);
                if (!pidofOptionAvailable)
                {
                    var regex = new Regex(@"\b\d+");
                    Match match = regex.Match(output);
                    if (!match.Success)
                        throw new Exception("Failed to parse pid");
                    output = match.Groups[0].Value;
                }

                return Int32.Parse(output);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                return -1;
            }
        }

        private int GetPIDFromPackageName(string packageName)
        {
            return GetPIDFromPackageName(packageName, m_SelectedDeviceId);
        }

        private bool GetCurrentPackage(ref string packageName, ref int packagePID)
        {
            if (string.IsNullOrEmpty(m_SelectedDeviceId))
                return false;
            try
            {
                var adb = GetCachedAdb();
                var cmd = $"-s {m_SelectedDeviceId} shell \"dumpsys activity | grep top-activity\" ";
                AndroidLogcatInternalLog.Log($"{adb.GetADBPath()} {cmd}");
                var output = adb.Run(new[] { cmd }, "Unable to get the top activity.");
                AndroidLogcatInternalLog.Log(output);
                if (output.Length == 0)
                    return false;

                var reg = new Regex(@"(\d{2,})\:([^/]*)");
                Match match = reg.Match(output);
                if (!match.Success)
                {
                    UnityEngine.Debug.Log("Match failed.");
                    return false;
                }

                packagePID = int.Parse(match.Groups[1].Value);
                packageName = match.Groups[2].Value;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<string> RetrieveConnectDevicesIDs(ADB adb)
        {
            var deviceIds = new List<string>();

            AndroidLogcatInternalLog.Log("{0} devices", adb.GetADBPath());
            var adbOutput = adb.Run(new[] { "devices" }, "Unable to list connected devices. ");
            foreach (var line in adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()))
            {
                AndroidLogcatInternalLog.Log(" " + line);
                if (line.EndsWith("device"))
                {
                    var deviceId = line.Substring(0, line.IndexOf('\t'));
                    deviceIds.Add(deviceId);
                }
            }

            return deviceIds;
        }

        private string GetDeviceDetailsFor(string deviceId)
        {
            var deviceIndex = m_DeviceIds.IndexOf(deviceId);
            System.Diagnostics.Debug.Assert(deviceIndex >= 0);

            return m_DeviceDetails[deviceIndex];
        }

        private string RetrieveDeviceDetailsFor(ADB adb, string deviceId)
        {
            var device = GetAndroidDeviceFromCache(adb, deviceId);
            if (device == null)
            {
                return deviceId;
            }

            var manufacturer = device.Properties["ro.product.manufacturer"];
            var model = device.Properties["ro.product.model"];
            var release = device.Properties["ro.build.version.release"];
            var sdkVersion = device.Properties["ro.build.version.sdk"];

            return $"{manufacturer} {model} (version: {release}, sdk: {sdkVersion}, id: {deviceId})";
        }

        private AndroidDevice GetAndroidDeviceFromCache(ADB adb, string deviceId)
        {
            AndroidDevice device;
            if (m_CachedDevices.TryGetValue(deviceId, out device))
            {
                return device;
            }

            try
            {
                device = new AndroidDevice(adb, deviceId);
                m_CachedDevices[deviceId] = device;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Exception caugth while trying to retrieve device details for device {deviceId}. This is harmless and device id will be used. Details\r\n:{ex}");
                // device will be null in this case (and it will not be added to the cache)
            }

            return device;
        }

        private void RestartLogCat()
        {
            StopLogCat();

            m_LogEntries.Clear();

            ConnectToDeviceId(m_SelectedDeviceId);
        }

        private void StopLogCat()
        {
            m_LogCat?.Stop();
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

        private void CaptureScreen()
        {
            if (string.IsNullOrEmpty(m_SelectedDeviceId))
                return;

            try
            {
                const string screenshotPathOnDevice = "/sdcard/screen.png";
                var adb = GetCachedAdb();

                // Capture the screen on the device.
                var cmd = $"-s {m_SelectedDeviceId} shell screencap {screenshotPathOnDevice}";
                var output = adb.Run(new[] {cmd}, $"Unable to capture the screen for device {m_SelectedDeviceId}.");
                if (output.StartsWith("Unable to capture the screen for device"))
                {
                    Debug.LogError(output);
                    return;
                }

                // Pull screenshot from the device to temp folder.
                var filePath = Path.Combine(Path.GetTempPath(), "screen_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png");
                cmd = $"-s {m_SelectedDeviceId} pull {screenshotPathOnDevice} {filePath}";
                output = adb.Run(new[] { cmd }, $"Unable to pull the screenshot from device {m_SelectedDeviceId}.");
                if (output.StartsWith("Unable to pull the screenshot from device"))
                {
                    Debug.LogError(output);
                    return;
                }

                AndroidLogcatScreenCaptureWindow.Show(filePath);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Exception caugth while capturing screen on device {m_SelectedDeviceId}. Details\r\n:{ex}");
            }
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
    }
}
