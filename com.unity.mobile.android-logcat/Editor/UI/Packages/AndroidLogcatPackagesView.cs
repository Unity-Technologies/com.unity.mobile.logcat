using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackagesView
    {
        enum PackagesContextMenu
        {
            CopyPackageName,
            CopyPackageInfo,
            CopyAll
        }

        IAndroidLogcatDevice m_Device;
        MultiColumnListView m_ListView;
        TextField m_Filter;

        List<PackageEntry> m_UnfilteredEntries;
        List<PackageEntry> m_FilteredEntries;

        internal Action<PackageEntry> PackageSelected { set; get; }
        internal Action<PackageEntry> PackageUninstalled { set; get; }

        internal AndroidLogcatPackagesView(AndroidLogcatRuntimeBase runtime, VisualElement root, List<PackageEntry> packageEntries)
        {
            m_UnfilteredEntries = packageEntries;
            m_FilteredEntries = new List<PackageEntry>();

            m_ListView = root.Q<MultiColumnListView>("packages");
            m_Filter = root.Q<TextField>("filter");
            m_Filter.SetValueWithoutNotify(runtime.UserSettings.PackageWindowSettings.PacakgeFilter);
            m_Filter.RegisterValueChangedCallback((s) =>
            {
                runtime.UserSettings.PackageWindowSettings.PacakgeFilter = s.newValue;
                FilterBy(s.newValue);
            });

            m_ListView.sortingEnabled = true;
            m_ListView.columnSortingChanged += ColumnSortingChanged;
            CreateLabel(nameof(PackageEntry.Name), (e) => e.Name);
            CreateLabel(nameof(PackageEntry.Installer), (e) => e.Installer);
            CreateLabel(nameof(PackageEntry.UID), (e) => e.UID);

            var operations = m_ListView.columns["operations"];
            operations.makeCell = () =>
            {
                var operations = new VisualElement();
                operations.style.flexDirection = FlexDirection.Row;
                var start = new PackageEntryButton() { name = "start", text = "►", tooltip = "Launch or Resume the package" };
                start.RegisterCallback<ClickEvent>((e) =>
                {
                    m_Device.StartOrResumePackage(start.Entry.Name);
                });
                operations.Add(start);
                var uninstall = new PackageEntryButton() { name = "uninstall", text = "X", tooltip = "Uninstall the package" };
                uninstall.RegisterCallback<ClickEvent>((e) =>
                {
                    if (AndroidLogcatUtilities.UninstallPackageWithConfirmation(m_Device, uninstall.Entry))
                        PackageUninstalled?.Invoke(uninstall.Entry);
                });
                operations.Add(uninstall);
                return operations;
            };

            operations.bindCell = (element, index) =>
            {
                var entry = (PackageEntry)m_ListView.itemsSource[index];

                var play = element.Q<PackageEntryButton>("start");
                play.Entry = entry;
                play.Index = index;

                var uninstall = element.Q<PackageEntryButton>("uninstall");
                uninstall.Entry = entry;
                uninstall.Index = index;
                // Disable uninstall for packages installed from Store, this also includes system pacakges
                // We don't want users accidentally uninstalling important stuff
                uninstall.SetEnabled(string.IsNullOrEmpty(entry.Installer) || entry.Installer.Equals("null"));
            };

            FilterBy(m_Filter.value);
        }

        internal void RefreshEntries(IAndroidLogcatDevice device, List<PackageEntry> packageEntries)
        {
            m_Device = device;
            m_UnfilteredEntries = packageEntries;
            FilterBy(m_Filter.value);
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

        void CreateLabel(string name, Func<PackageEntry, string> getText, Func<PackageEntry, string> getTooltip = null)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () =>
            {
                var label = new PackageEntryLabel();
                label.style.marginLeft = 5.0f;
                label.RegisterCallback<MouseDownEvent, PackageEntryLabel>((e, l) =>
                {
                    // By default only left click applies the selection, select with right click too
                    if (e.button == 1)
                        m_ListView.SetSelectionWithoutNotify(new[] { l.Index });

                    PackageSelected?.Invoke(l.Entry);
                }, label);

                label.RegisterCallback<MouseUpEvent, PackageEntryLabel>((e, l) =>
                {
                    if (e.button != 1)
                        return;

                    m_ListView.SetSelection(l.Index);

                    var contextMenu = new AndroidContextMenu<PackagesContextMenu>();
                    contextMenu.Add(PackagesContextMenu.CopyPackageName, "Copy Package Name");
                    contextMenu.Add(PackagesContextMenu.CopyPackageInfo, "Copy Package Information");
                    contextMenu.Add(PackagesContextMenu.CopyAll, "Copy All");

                    contextMenu.Show(e.mousePosition, (userData, options, selected) =>
                    {
                        var sender = (AndroidContextMenu<PackagesContextMenu>)userData;
                        var item = sender.GetItemAt(selected);
                        if (item == null)
                            return;

                        switch (item.Item)
                        {
                            case PackagesContextMenu.CopyPackageName:
                                EditorGUIUtility.systemCopyBuffer = l.Entry.Name;
                                break;
                            case PackagesContextMenu.CopyPackageInfo:
                                EditorGUIUtility.systemCopyBuffer = l.Entry.ToString();
                                break;
                            case PackagesContextMenu.CopyAll:
                                var data = new StringBuilder();
                                foreach (var p in m_FilteredEntries)
                                {
                                    data.AppendLine(p.ToString());
                                }
                                EditorGUIUtility.systemCopyBuffer = data.ToString();
                                break;
                        }
                    });

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

        T GetInitializedElement<T>(VisualElement element, int index) where T : PackageEntryVisualElement
        {
            var packageEntryElement = (PackageEntryVisualElement)element;
            packageEntryElement.Entry = (PackageEntry)m_ListView.itemsSource[index];
            packageEntryElement.Index = index;
            return (T)packageEntryElement;
        }

    }
}
