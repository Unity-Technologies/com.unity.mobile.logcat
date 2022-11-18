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
    internal class AndroidLogcatPackageListViewState<T> where T : Enum
    {
        internal enum Columns
        {
            PackageName,
            Installer,
            UniqueIdentifier,
            Operations
        }

        [SerializeField]
        internal TreeViewState treeViewState;

        [SerializeField]
        internal MultiColumnHeaderState columnHeaderState;

        [NonSerialized]
        internal MultiColumnHeader columnHeader;

        internal static AndroidLogcatPackageListViewState<T> CreateOrInitializeViewState(AndroidLogcatPackageListViewState<T> state = null)
        {
            if (state == null)
                state = new AndroidLogcatPackageListViewState<T>();

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

        protected static MultiColumnHeaderState.Column CreateColumn(string name, float width)
        {
            return new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(name),
                headerTextAlignment = TextAlignment.Left,
                canSort = false,
                width = width,
                minWidth = width,
                autoResize = true,
                allowToggleVisibility = false
            };
        }

        private static MultiColumnHeaderState CreateMultiColumnHeaderState()
        {
            var columns = new[]
            {
                CreateColumn("Package Name", 250),
                CreateColumn("Installer", 200),
                CreateColumn("UID", 50),
                CreateColumn("Operations", 50)
            };
            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(T)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            return new MultiColumnHeaderState(columns);
        }
    }
}
