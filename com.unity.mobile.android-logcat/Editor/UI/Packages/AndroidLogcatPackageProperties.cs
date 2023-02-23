using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageProperties
    {
        MultiColumnListView m_ListView;
        TextField m_Filter;

        List<string> m_UnfilteredEntries;
        List<string> m_FilteredEntries;

        internal AndroidLogcatPackageProperties(VisualElement root, List<string> properties = null)
        {
            m_UnfilteredEntries = properties;
            if (m_UnfilteredEntries == null)
                m_UnfilteredEntries = new List<string>();
            m_FilteredEntries = new List<string>();

            m_ListView = root.Q<MultiColumnListView>("package_properties");
            m_Filter = root.Q<TextField>("package_properties_filter");

            // TODO: take filter from settings
            m_Filter.RegisterValueChangedCallback((s) =>
            {
                FilterBy(s.newValue);
            });

            m_ListView.itemsSource = m_UnfilteredEntries;
            CreateLabel("value");
            // CreateLabel("value", (e) => e.Value, (e) => $"{e.Key} = {e.Value}");
        }

        internal void RefreshProperties(List<string> packageEntries)
        {
            m_UnfilteredEntries = packageEntries;
            FilterBy(m_Filter.value);
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
                    if (e.Contains(filter))
                        m_FilteredEntries.Add(e);
                }
            }

            m_ListView.itemsSource = m_FilteredEntries;
            m_ListView.RefreshItems();
        }

        void CreateLabel(string name)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () => new Label()
            {
                style =
                {
                    marginLeft = 5.0f
                }
            };
            m_ListView.columns[id].bindCell = (element, index) =>
            {
                var label = (Label)element;
                var entry = (string)m_ListView.itemsSource[index];
                label.text = entry;
                label.tooltip = entry.Trim();
            };
        }

    }
}
