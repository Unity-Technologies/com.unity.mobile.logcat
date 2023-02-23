using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackages
    {
        MultiColumnListView m_ListView;
        TextField m_Filter;

        List<PackageEntry> m_UnfilteredEntries;
        List<PackageEntry> m_FilteredEntries;

        internal Action<PackageEntry> PackageSelected { set; get; }

        internal AndroidLogcatPackages(VisualElement root, List<PackageEntry> packageEntries)
        {
            m_UnfilteredEntries = packageEntries;
            m_FilteredEntries = new List<PackageEntry>();

            m_ListView = root.Q<MultiColumnListView>("packages");
            m_Filter = root.Q<TextField>("filter");
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
        }

        internal void RefreshEntries(List<PackageEntry> packageEntries)
        {
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
                    PackageSelected?.Invoke(l.Entry);
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
            return (T)packageEntryElement;
        }

    }
}
