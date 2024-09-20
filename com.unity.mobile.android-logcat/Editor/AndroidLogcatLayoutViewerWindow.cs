using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatLayoutViewerWindow : EditorWindow
    {
        class Styles
        {
            public static GUIContent QueryUIHierarchy = new GUIContent("Query UI Layout");
        }

        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatDeviceSelection m_DeviceSelection;
        AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        AndroidLogcatQueryLayout m_QueryLayout;
        TreeView m_LayoutNodesTreeView;

        internal static void ShowWindow()
        {
            var window = (AndroidLogcatLayoutViewerWindow)EditorWindow.GetWindow(typeof(AndroidLogcatLayoutViewerWindow));
            window.titleContent = new GUIContent("Layout Viewer");
            window.Show();
        }

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (rootVisualElement == null)
                throw new NullReferenceException("rooVisualElement is null");

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.Closing += OnDisable;
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, OnDeviceSelected);
            m_CaptureScreenshot = m_Runtime.CaptureScreenshot;
            m_QueryLayout = m_Runtime.QueryLayout;

            LoadUI();

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
        }

        private void LoadUI()
        {
            var r = rootVisualElement;
            if (Unsupported.IsDeveloperMode())
                r.Insert(0, new IMGUIContainer(DoDebuggingGUI));
            r.Insert(0, new IMGUIContainer(DoToolbarGUI));


            var tree = AndroidLogcatUtilities.LoadUXML("AndroidLogcatLayoutViewer.uxml");
            tree.CloneTree(r);

            r.Q<IMGUIContainer>("LayoutImage").onGUIHandler = DoLayoutImage;
            m_LayoutNodesTreeView = r.Q<TreeView>("LayoutNodes");

            m_LayoutNodesTreeView.makeItem = () => new Label();
            m_LayoutNodesTreeView.bindItem = (v, i) =>
            {
                var item = m_LayoutNodesTreeView.GetItemDataForIndex<AndroidLogcatQueryLayout.LayoutNode>(i);
                ((Label)v).text = item.ClassName;
            };
        }

        private void OnDisable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime == null)
                return;
            if (m_DeviceSelection != null)
            {
                m_DeviceSelection.Dispose();
                m_DeviceSelection = null;
            }
            m_Runtime = null;
        }

        private void OnDeviceSelected(IAndroidLogcatDevice selectedDevice)
        {
            // TODO:
        }

        private void OnQueryLayout()
        {
            List<TreeViewItemData<AndroidLogcatQueryLayout.LayoutNode>> GetItems(IReadOnlyList<AndroidLogcatQueryLayout.LayoutNode> nodes)
            {
                if (nodes == null)
                    return null;

                var items = new List<TreeViewItemData<AndroidLogcatQueryLayout.LayoutNode>>();
                foreach (var node in nodes)
                {
                    items.Add(new TreeViewItemData<AndroidLogcatQueryLayout.LayoutNode>(node.Id, node, GetItems(node.Childs)));
                }
                return items;
            }


            m_LayoutNodesTreeView.SetRootItems<AndroidLogcatQueryLayout.LayoutNode>(GetItems(m_QueryLayout.Nodes));
            m_LayoutNodesTreeView.RefreshItems();
            m_LayoutNodesTreeView.ExpandAll();
        }

        void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Label(GUIContent.none, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));
            EditorGUI.EndDisabledGroup();
            m_DeviceSelection.DoGUI();
            if (GUILayout.Button(Styles.QueryUIHierarchy, AndroidLogcatStyles.toolbarButton))
            {
                m_CaptureScreenshot.QueueScreenCapture(m_DeviceSelection.SelectedDevice, Repaint);
                m_QueryLayout.QueueCaptureLayout(m_DeviceSelection.SelectedDevice, OnQueryLayout);
            }
            EditorGUILayout.EndHorizontal();
        }

        void DoDebuggingGUI()
        {
            GUILayout.Label("Developer Mode is on, showing debugging buttons:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            if (GUILayout.Button("Reload Me", AndroidLogcatStyles.toolbarButton))
            {
                EditorUtility.RequestScriptReload();
            }

            if (GUILayout.Button("Reload UI", AndroidLogcatStyles.toolbarButton))
            {
                rootVisualElement.Clear();
                LoadUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        void DoLayoutImage()
        {
            var rc = GUILayoutUtility.GetRect(0, Screen.width, 0, Screen.height);
            m_CaptureScreenshot.DoGUI(rc);
        }
    }
}
