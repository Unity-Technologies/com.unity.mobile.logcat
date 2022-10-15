using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Android.Logcat
{
    // Note: UI Toolkit MulticolumnListView is not available in 2020
    [Serializable]
    internal class AndroidLogcatPackageListViewState
    {
        internal enum Columns
        {
            PackageName,
            UniqueIdentifier,
            Installer
        }

        [SerializeField]
        internal TreeViewState treeViewState;

        [SerializeField]
        internal MultiColumnHeaderState columnHeaderState;

        [NonSerialized]
        internal MultiColumnHeader columnHeader;

        internal static AndroidLogcatPackageListViewState CreateOrInitializeTreeState(AndroidLogcatPackageListViewState state = null)
        {
            if (state == null)
                state = new AndroidLogcatPackageListViewState();

            if (state.treeViewState == null)
                state.treeViewState = new TreeViewState();

            bool firstInit = state.columnHeaderState == null;
            var headerState = CreateMultiColumnHeaderState();
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(state.columnHeaderState, headerState))
                MultiColumnHeaderState.OverwriteSerializedFields(state.columnHeaderState, headerState);
            state.columnHeaderState = headerState;

            state.columnHeader = new MultiColumnHeader(headerState);
            if (firstInit)
                state.columnHeader.ResizeToFit();

            return state;
        }

        private static MultiColumnHeaderState CreateMultiColumnHeaderState()
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Package Name"),
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    width = 200,
                    minWidth = 200,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("UID", "Unique Identifier"),
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    width = 200,
                    minWidth = 200,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Installer", "The installer application"),
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    width = 100,
                    minWidth = 100,
                    autoResize = false,
                    allowToggleVisibility = false
                }
            };

            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(Columns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            var header = new MultiColumnHeaderState(columns);
            return header;
        }
    }
}
