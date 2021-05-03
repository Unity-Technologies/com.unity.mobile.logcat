using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal enum ContextMenuItem
    {
        None,
        Copy,
        SelectAll,
        SaveSelection,
        AddTag,
        RemoveTag,
        FilterByProcessId
    }

    class AndroidContextMenu
    {
        internal class MenuItemData
        {
            public ContextMenuItem Item { get; }
            public string Name { get; }

            public MenuItemData(ContextMenuItem item, string name)
            {
                Item = item;
                Name = name;
            }
        }

        public object UserData { set; get; }

        private List<MenuItemData> m_Items = new List<MenuItemData>();

        public void Add(ContextMenuItem item, string name)
        {
            m_Items.Add(new MenuItemData(item, name));
        }

        public void AddSplitter()
        {
            Add(ContextMenuItem.None, string.Empty);
        }

        public string[] Names => m_Items.Select(i => i.Name).ToArray();

        public MenuItemData GetItemAt(int selected)
        {
            if (selected < 0 || selected > m_Items.Count - 1)
                return null;
            return m_Items[selected];
        }

        public void Show(Vector2 mousePosition, EditorUtility.SelectMenuItemFunction callback)
        {
            var enabled = Enumerable.Repeat(true, Names.Length).ToArray();
            var separator = new bool[Names.Length];
            EditorUtility.DisplayCustomMenuWithSeparators(new Rect(mousePosition.x, mousePosition.y, 0, 0),
                Names,
                enabled,
                separator,
                null,
                callback,
                this);
        }
    }
}
