using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    // adb shell dumpsys package <package>

    // adb shell cmd package list packages
    // list packages[-f] [-d]
    //    [-e]
    //    [-s]
    //    [-3]
    //    [-i]
    //    [-l]
    //    [-u]
    //    [-U]
    //    [--uid UID]
    //    [--user USER_ID]
    //    [FILTER]
    // Prints all packages; optionally only those whose name contains the text in FILTER.
    // Options:
    // -f: see their associated file
    // -d: filter to only show disabled packages
    // -e: filter to only show enabled packages
    // -s: filter to only show system packages
    // -3: filter to only show third party packages
    // -i: see the installer for the packages
    // -l: ignored(used for compatibility with older releases)
    // -U: also show the package UID
    // -u: also include uninstalled packages
    // --uid UID: filter to only show packages with the given UID
    // --user USER_ID: only list packages belonging to the given user

    // Example., adb shell cmd package list packages -3 -U -i

    internal class AndroidLogcatPackagesWindow : EditorWindow
    {
        private GUIContent kRefresh = new GUIContent(L10n.Tr("Refresh"), L10n.Tr("Refresh package list."));

        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatPackagesView m_Packages;
        AndroidLogcatPackagePropertiesView m_PackageProperties;
        AndroidLogcatPackageUtilities m_PackageUtilities;
        AndroidLogcatDeviceSelection m_DeviceSelection;
        TwoPaneSplitView m_HorizontalSplit;
        TwoPaneSplitView m_VerticalSplit;

        internal static void ShowWindow()
        {
            // Get existing open window or if none, make a new one:
            AndroidLogcatPackagesWindow window = (AndroidLogcatPackagesWindow)EditorWindow.GetWindow(typeof(AndroidLogcatPackagesWindow));
            window.titleContent = new UnityEngine.GUIContent("Pacakge Information");
            window.Show();
        }

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (rootVisualElement == null)
                throw new NullReferenceException("rooVisualElement is null");
            rootVisualElement.Insert(0, new IMGUIContainer(DoDebuggingGUI));
            rootVisualElement.Insert(0, new IMGUIContainer(DoToolbarGUI));

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.Closing += OnDisable;
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, OnDeviceSelected);

            // TODO: Add query for uxml + clone
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.mobile.android-logcat/Editor/UI/Layouts/AndroidLogcatPackagesLayout.uxml");
            tree.CloneTree(rootVisualElement);

            m_HorizontalSplit = rootVisualElement.Q<TwoPaneSplitView>("HorizontalSplit");
            m_HorizontalSplit.RegisterCallback<GeometryChangedEvent>(InitializeHorizontalLayout);

            m_VerticalSplit = rootVisualElement.Q<TwoPaneSplitView>("VerticalSplit");
            m_VerticalSplit.RegisterCallback<GeometryChangedEvent>(InitializeVerticalLayout);

            m_Packages = new AndroidLogcatPackagesView(m_Runtime, rootVisualElement, GetPackageEntries(m_DeviceSelection.SelectedDevice).ToList());
            m_Packages.PackageSelected = PackageSelected;
            m_PackageProperties = new AndroidLogcatPackagePropertiesView(rootVisualElement);
            m_PackageUtilities = new AndroidLogcatPackageUtilities(rootVisualElement);

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
        }

        private void OnDisable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime == null)
                return;
            if (m_DeviceSelection != null)
            {
                m_DeviceSelection.Dispose();
                m_DeviceSelection = null;
            }
            m_Runtime = null;
        }

        private void OnDeviceSelected(IAndroidLogcatDevice selectedDevice)
        {
            if (m_Packages == null)
                throw new Exception("Package view was not created ?");
            m_Packages.RefreshEntries(selectedDevice, GetPackageEntries(selectedDevice).ToList());
        }

        PackageEntry[] GetPackageEntries(IAndroidLogcatDevice selectedDevice)
        {
            if (selectedDevice == null)
                return Array.Empty<PackageEntry>();

            return AndroidLogcatUtilities.RetrievePackages(
                m_Runtime.Tools.ADB,
                selectedDevice);
        }

        private void InitializeHorizontalLayout(GeometryChangedEvent e)
        {
            m_HorizontalSplit.fixedPaneInitialDimension = m_HorizontalSplit.layout.width / 2;
            m_HorizontalSplit.UnregisterCallback<GeometryChangedEvent>(InitializeHorizontalLayout);
        }

        private void InitializeVerticalLayout(GeometryChangedEvent e)
        {
            m_VerticalSplit.fixedPaneInitialDimension = m_VerticalSplit.layout.height * 0.8f;
            m_VerticalSplit.UnregisterCallback<GeometryChangedEvent>(InitializeVerticalLayout);
        }

        void PackageSelected(PackageEntry entry)
        {
            // TODO: device selection

            var parser = new AndroidLogcatPackageInfoParser(AndroidLogcatUtilities.RetrievePackageProperties(
                m_Runtime.Tools.ADB, m_Runtime.DeviceQuery.FirstConnectedDevice, entry));
            var entries = parser.ParsePackageInformationAsSingleEntries(entry.Name);
            m_PackageProperties.RefreshProperties(entries);

            var activities = parser.ParseLaunchableActivities(entry.Name);
            m_PackageUtilities.RefreshActivities(activities);

        }

        void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Label(GUIContent.none, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));
            EditorGUI.EndDisabledGroup();
            m_DeviceSelection.DoGUI();
            if (GUILayout.Button(kRefresh, AndroidLogcatStyles.toolbarButton))
                OnDeviceSelected(m_DeviceSelection.SelectedDevice);
            EditorGUILayout.EndHorizontal();
        }

        void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing debugging buttons:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            if (GUILayout.Button("Reload Me", AndroidLogcatStyles.toolbarButton))
            {
                EditorUtility.RequestScriptReload();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
