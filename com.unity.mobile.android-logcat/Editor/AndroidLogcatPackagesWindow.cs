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
        MultiColumnListView m_ListView;
        AndroidLogcatRuntimeBase m_Runtime;
        TextField m_Filter;

        List<PackageEntry> m_UnfilteredEntries;
        List<PackageEntry> m_FilteredEntries;

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
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.DeviceQuery.DevicesUpdated += UpdateEntries;

            // No device selected yet
            m_FilteredEntries = new List<PackageEntry>();
            m_UnfilteredEntries = new List<PackageEntry>();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.mobile.android-logcat/Editor/UI/Layouts/AndroidLogcatPackagesLayout.uxml");
            tree.CloneTree(rootVisualElement);

            rootVisualElement.Q<TwoPaneSplitView>().RegisterCallback<GeometryChangedEvent>(InitializeLayout);

            m_ListView = rootVisualElement.Q<MultiColumnListView>();
            m_Filter = rootVisualElement.Q<TextField>("filter");
            // TODO: take filter from settings
            m_Filter.RegisterValueChangedCallback((s) =>
            {
                FilterBy(s.newValue);
            });

            m_ListView.itemsSource = m_UnfilteredEntries;
            m_ListView.sortingEnabled = true;
            m_ListView.columnSortingChanged += ColumnSortingChanged;
            CreateLabel(nameof(PackageEntry.Name), (e) => e.Name);
            CreateLabel(nameof(PackageEntry.Installer), (e) => e.Installer);
            CreateLabel(nameof(PackageEntry.UID), (e) => e.UID);

            rootVisualElement.Insert(0, new IMGUIContainer(DoDebuggingGUI));
        }

        internal void InitializeLayout(GeometryChangedEvent e)
        {
            var split = rootVisualElement.Q<TwoPaneSplitView>();
            split.fixedPaneInitialDimension = split.layout.width / 2;
            split.UnregisterCallback<GeometryChangedEvent>(InitializeLayout);
        }

        private void ColumnSortingChanged()
        {
            var column = m_ListView.sortedColumns.FirstOrDefault();
            m_FilteredEntries.Sort((x, y) =>
            {
                var result = 0;
                if (column.columnName.Equals(nameof(PackageEntry.Name), StringComparison.InvariantCultureIgnoreCase))
                {
                    result = x.Name.CompareTo(y.Name);
                }
                else if (column.columnName.Equals(nameof(PackageEntry.Installer), StringComparison.InvariantCultureIgnoreCase))
                {
                    result = x.Installer.CompareTo(y.Installer);
                }
                else if (column.columnName.Equals(nameof(PackageEntry.UID), StringComparison.InvariantCultureIgnoreCase))
                {
                    result = x.UID.CompareTo(y.UID);
                }

                if (column.direction == SortDirection.Descending)
                    return result;
                return -result;
            });

            m_ListView.itemsSource = m_FilteredEntries;
            m_ListView.RefreshItems();
        }

        private void FilterBy(string filter)
        {
            m_FilteredEntries.Clear();
            if (string.IsNullOrEmpty(filter))
            {
                m_FilteredEntries.AddRange(m_UnfilteredEntries);
            }
            else
            {
                foreach (var e in m_UnfilteredEntries)
                {
                    if (e.Name.Contains(filter) ||
                        e.Installer.Contains(filter) ||
                        e.UID.Contains(filter))
                        m_FilteredEntries.Add(e);
                }
            }

            m_ListView.itemsSource = m_FilteredEntries;
            m_ListView.RefreshItems();
        }

        private void UpdateEntries()
        {
            m_UnfilteredEntries = GetPackageEntries().ToList();
            FilterBy(m_Filter.value);
        }

        void CreateLabel(string name, Func<PackageEntry, string> getText, Func<PackageEntry, string> getTooltip = null)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () =>
            {
                var label = new PackageEntryLabel();
                label.RegisterCallback<MouseDownEvent, PackageEntryLabel>((e, l) =>
                {
                    switch (e.button)
                    {
                        case 0:
                            if (e.clickCount == 2)
                            {
                                //OnSelectEntryInListView(l.Entry);
                            }
                            break;
                    }
                }, label);
                return label;
            };

            m_ListView.columns[id].bindCell = (element, index) =>
            {
                var label = GetInitializedElement<PackageEntryLabel>(element, index);
                label.text = getText(label.Entry);
                if (getTooltip != null)
                    label.tooltip = getTooltip(label.Entry);
            };
        }

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

        T GetInitializedElement<T>(VisualElement element, int index) where T : PackageEntryVisualElement
        {
            var packageEntryElement = (PackageEntryVisualElement)element;
            packageEntryElement.Entry = (PackageEntry)m_ListView.itemsSource[index];
            return (T)packageEntryElement;
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
