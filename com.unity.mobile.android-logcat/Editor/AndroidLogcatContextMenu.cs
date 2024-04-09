using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal enum MessagesContextMenu
    {
        None,
        Copy,
        SelectAll,
        SaveSelection,
        AddTag,
        RemoveTag,
        FilterByProcessId,
        SendUnixSignal,
        CrashProcess,
        ForceStop,
        SendTrimMemory
    }

    internal enum ToolsContextMenu
    {
        None,
        ScreenCapture,
        OpenTerminal,
        StacktraceUtility,
        WindowMemory,
        WindowInputs,
        WindowHidden
    }

    internal enum FilterContextMenu
    {
        UseRegularExpressions,
        MatchCase
    }

    class AndroidContextMenu<T>
    {
        internal class MenuItemData
        {
            public T Item { get; }
            public string Name { get; }
            public bool Selected { get; }
            public bool Enabled { get; }
            public object UserData { get; }

            public MenuItemData(T item, string name, bool selected, bool enabled, object userData)
            {
                Item = item;
                Name = name;
                Selected = selected;
                Enabled = enabled;
                UserData = userData;

            }
        }

        public object UserData { set; get; }

        private List<MenuItemData> m_Items = new List<MenuItemData>();

        public void Add(T item, string name, bool selected = false, bool enabled = true, object userData = null)
        {
            m_Items.Add(new MenuItemData(item, name, selected, enabled, userData));
        }

        public void AddSplitter()
        {
            Add(default, string.Empty);
        }

        public string[] Names => m_Items.Select(i => i.Name).ToArray();

        private int[] Selected
        {
            get
            {
                var selected = new List<int>();
                for (int i = 0; i < m_Items.Count; i++)
                {
                    if (!m_Items[i].Selected)
                        continue;
                    selected.Add(i);
                }

                return selected.ToArray();
            }
        }

        public MenuItemData GetItemAt(int selected)
        {
            if (selected < 0 || selected > m_Items.Count - 1)
                return null;
            return m_Items[selected];
        }

        public void Show(Vector2 mousePosition, EditorUtility.SelectMenuItemFunction callback)
        {
            var enabled = m_Items.Select(i => i.Enabled).ToArray();
            var separator = new bool[Names.Length];
            EditorUtility.DisplayCustomMenuWithSeparators(new Rect(mousePosition.x, mousePosition.y, 0, 0),
                Names,
                enabled,
                separator,
                Selected,
                callback,
                this);
        }
    }
}
