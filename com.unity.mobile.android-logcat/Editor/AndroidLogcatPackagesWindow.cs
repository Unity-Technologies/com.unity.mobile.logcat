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
        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatPackages m_Packages;
        AndroidLogcatPackageProperties m_PackageProperties;

        [MenuItem("Test/Test")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            AndroidLogcatPackagesWindow window = (AndroidLogcatPackagesWindow)EditorWindow.GetWindow(typeof(AndroidLogcatPackagesWindow));
            window.titleContent = new UnityEngine.GUIContent("Packages");
            window.Show();
        }

        private void OnEnable()
        {
            if (rootVisualElement == null)
                throw new NullReferenceException("rooVisualElement is null");
            var label = new Label();
            label.text = "sdsd\nsdsd\n";
            rootVisualElement.Insert(0, label);
            rootVisualElement.Insert(0, new IMGUIContainer(DoDebuggingGUI));



            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.DeviceQuery.DevicesUpdated += UpdateEntries;


            // TODO: Add query for uxml + clone
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.mobile.android-logcat/Editor/UI/Layouts/AndroidLogcatPackagesLayout.uxml");
            tree.CloneTree(rootVisualElement);

            rootVisualElement.Q<TwoPaneSplitView>().RegisterCallback<GeometryChangedEvent>(InitializeLayout);

            m_Packages = new AndroidLogcatPackages(rootVisualElement, GetPackageEntries().ToList());
            m_Packages.PackageSelected = PackageSelected;
            m_PackageProperties = new AndroidLogcatPackageProperties(rootVisualElement);
        }

        private void UpdateEntries()
        {
            m_Packages.RefreshEntries(GetPackageEntries().ToList());
        }

        internal void InitializeLayout(GeometryChangedEvent e)
        {
            var split = rootVisualElement.Q<TwoPaneSplitView>();
            split.fixedPaneInitialDimension = split.layout.width / 2;
            split.UnregisterCallback<GeometryChangedEvent>(InitializeLayout);
        }

        /*
        void CreateButton(string name)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () => new PackageEntryButton();
            m_ListView.columns[id].bindCell = (element, index) =>
            {
                var button = GetInitializedElement<PackageEntryButton>(element, index);
                button.text = "Hello";
            };
        }
        */

        void PackageSelected(PackageEntry entry)
        {
            // TODO: device selection
            var entries = AndroidLogcatUtilities.RetrievePackageProperties(
                m_Runtime.Tools.ADB,
                m_Runtime.DeviceQuery.FirstConnectedDevice,
                entry);
            m_PackageProperties.RefreshProperties(entries);
        }

        PackageEntry[] GetPackageEntries()
        {
            return AndroidLogcatUtilities.RetrievePackages(
                m_Runtime.Tools.ADB,
                m_Runtime.DeviceQuery.FirstConnectedDevice);
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
