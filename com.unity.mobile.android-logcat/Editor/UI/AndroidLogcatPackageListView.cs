using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.Android.Logcat
{
    // Note: UI Toolkit MulticolumnListView is not available in 2020
    internal class AndroidLogcatPackageListView : TreeView
    {
        internal static class Styles
        {
            internal static GUIStyle buildSettingsButton = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };
        }

        private Dictionary<int, AndroidLogcatPackageListItem> m_CachedRowMap = new Dictionary<int, AndroidLogcatPackageListItem>();
        private IReadOnlyList<PackageEntry> m_Entries;

        public AndroidLogcatPackageListView(AndroidLogcatPackageListViewState state, IReadOnlyList<PackageEntry> entries)
            : base(state.treeViewState, state.columnHeader)
        {
            m_Entries = entries;

            rowHeight = 20f;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (rowHeight - EditorGUIUtility.singleLineHeight) * 0.5f;
            extraSpaceBeforeIconAndLabel = 18f;


            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            return new TreeViewItem(0, -1);
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var items = new List<TreeViewItem>();
            foreach (var e in m_Entries)
            {
                items.Add(new AndroidLogcatPackageListItem(0, e));
            }
            return items;
            /*
            var tempRoot = new AndroidLogcatPackageListItem(-1, null);

            foreach (var e in m_Entries)
            {
                tempRoot.AddChild(new AndroidLogcatPackageListItem(0, e));
            }

            var items = new List<TreeViewItem>();
            AddChildrenRecursive(tempRoot, -1, items);

            SetupParentsAndChildrenFromDepths(root, items);
            return items;
            */
        }

        /*
        void AddChildrenRecursive(TreeViewItem parent, int depth, IList<TreeViewItem> newRows)
        {
            if (parent == null || !parent.hasChildren)
                return;

            if (parent.children == null)
            {
                var fileTreeView = (AndroidLogcatPackageListItem)parent;
                var childs = m_QueryChilds(fileTreeView.DeviceExplorerEntry, parent.depth + 1);
                foreach (var c in childs)
                {
                    parent.AddChild(c);
                }
            }

            if (parent.children == null)
                return;

            foreach (AndroidLogcatPackageListItem child in parent.children)
            {
                var item = new AndroidLogcatPackageListItem(child.depth, child.DeviceExplorerEntry);
                newRows.Add(child);

                if (child.hasChildren)
                {
                    if (IsExpanded(child.id))
                    {
                        AddChildrenRecursive(child, depth + 1, newRows);
                    }
                    else
                    {
                        item.children = CreateChildListForCollapsedParent();
                    }
                }
            }
        }
        */

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (AndroidLogcatPackageListItem)args.item;
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                Rect rc = args.GetCellRect(i);
                if (i == columnIndexForTreeFoldouts)
                {
                    var indent = GetContentIndent(item);
                    rc.x += indent;
                    rc.width -= indent;
                }
                DisplayItem(item, i, rc);
            }
        }

        private void DisplayItem(AndroidLogcatPackageListItem item, int c, Rect rc)
        {
            AndroidLogcatPackageListViewState.Columns column = (AndroidLogcatPackageListViewState.Columns)c;

            var props = item.PackageEntry;
            EditorGUI.BeginChangeCheck();
            switch (column)
            {
                case AndroidLogcatPackageListViewState.Columns.PackageName:
                    GUI.Label(rc, new GUIContent(props.Name, props.Name));

                    break;
                case AndroidLogcatPackageListViewState.Columns.UniqueIdentifier:
                    GUI.Label(rc, new GUIContent(props.UID));

                    break;
                case AndroidLogcatPackageListViewState.Columns.Installer:
                    GUI.Label(rc, new GUIContent(props.Installer));

                    break;

                    /*
                case AndroidLogcatPackageListViewState.Columns.Permissions:
                    GUI.Label(rc, props.Permissions);
                    //props.Build = EditorGUI.Toggle(rc, props.Build);
                    break;
                case AndroidLogcatPackageListViewState.Columns.Date:
                    GUI.Label(rc, props.DateModified.ToString("yyyy-MM-dd HH:mm:ss"));
                    break;
                case AndroidLogcatPackageListViewState.Columns.Size:
                    GUI.Label(rc, $"{props.Size.ToString()}B");
                    break;
                    */
            }

            if (EditorGUI.EndChangeCheck())
                this.SetSelection(new List<int>(new[] { props.GetId() }), TreeViewSelectionOptions.None);
        }
    }
}
