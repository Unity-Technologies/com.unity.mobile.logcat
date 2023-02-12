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

        List<KeyValuePair<string, string>> m_UnfilteredEntries;
        List<KeyValuePair<string, string>> m_FilteredEntries;

        internal AndroidLogcatPackageProperties(VisualElement root, List<KeyValuePair<string, string>> properties = null)
        {
            m_UnfilteredEntries = properties;
            if (m_UnfilteredEntries == null)
                m_UnfilteredEntries = new List<KeyValuePair<string, string>>();
            m_FilteredEntries = new List<KeyValuePair<string, string>>();

            m_ListView = root.Q<MultiColumnListView>("package_properties");
            m_Filter = root.Q<TextField>("package_properties_filter");

            // TODO: take filter from settings
            m_Filter.RegisterValueChangedCallback((s) =>
            {
                FilterBy(s.newValue);
            });

            m_ListView.itemsSource = m_UnfilteredEntries;
            CreateLabel("name", (e) => e.Key, (e) => $"{e.Key} = {e.Value}");
            CreateLabel("value", (e) => e.Value, (e) => $"{e.Key} = {e.Value}");
        }

        internal void RefreshProperties(List<KeyValuePair<string, string>> packageEntries)
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
                    if (e.Key.Contains(filter) ||
                        e.Value.Contains(filter))
                        m_FilteredEntries.Add(e);
                }
            }

            m_ListView.itemsSource = m_FilteredEntries;
            m_ListView.RefreshItems();
        }

        void CreateLabel(string name,
            Func<KeyValuePair<string, string>, string> getText,
            Func<KeyValuePair<string, string>, string> getTooltip = null)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () => new Label();
            m_ListView.columns[id].bindCell = (element, index) =>
            {
                var label = (Label)element;
                var entry = (KeyValuePair<string, string>)m_ListView.itemsSource[index];
                label.text = getText(entry);
                if (getTooltip != null)
                    label.tooltip = getTooltip(entry);
            };
        }

    }
}
