using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatLayoutViewerWindow : EditorWindow
    {
        const string Name = nameof(Name);
        const string Value = nameof(Value);

        class Styles
        {
            public static GUIContent QueryUIHierarchy = new GUIContent("Query UI Layout");
        }

        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatDeviceSelection m_DeviceSelection;
        AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        AndroidLogcatQueryLayout m_QueryLayout;
        TreeView m_LayoutNodesTreeView;
        MultiColumnListView m_LayoutNodeValues;
        AndroidLogcatQueryLayout.LayoutNode m_SelectedNode;
        Vector2 m_CacheDisplaySize;

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

            // Setup layout nodes tree view
            {
                m_LayoutNodesTreeView = r.Q<TreeView>("LayoutNodes");
                m_LayoutNodesTreeView.makeItem = () => new Label();
                m_LayoutNodesTreeView.bindItem = (v, i) =>
                {
                    var item = m_LayoutNodesTreeView.GetItemDataForIndex<AndroidLogcatQueryLayout.LayoutNode>(i);
                    ((Label)v).text = item.ClassName;
                    ((Label)v).tooltip = item.Bounds.ToString();
                };
                m_LayoutNodesTreeView.selectionChanged += (IEnumerable<object> objs) =>
                {
                    if (objs.Count() == 0)
                    {
                        m_SelectedNode = null;
                        m_LayoutNodeValues.itemsSource = Array.Empty<AndroidLogcatQueryLayout.LayoutNode>();
                    }
                    else
                    {
                        var item = (AndroidLogcatQueryLayout.LayoutNode)objs.First();
                        m_LayoutNodeValues.itemsSource = item.Values.ToArray();
                        m_SelectedNode = item;
                    }
                    m_LayoutNodeValues.RefreshItems();
                };

                RefreshTreeView();
            }

            // Setup listview for node values
            {
                m_LayoutNodeValues = r.Q<MultiColumnListView>("NodeValues");
                m_LayoutNodeValues.RegisterCallback<MouseUpEvent>((e) =>
                {
                    if (e.button != 1)
                        return;
                    var contextMenu = new AndroidContextMenu<MessagesContextMenu>();
                    contextMenu.Add(MessagesContextMenu.Copy, "Copy");
                    contextMenu.Add(MessagesContextMenu.CopyAll, "Copy All");
                    contextMenu.Show(e.mousePosition, (userData, options, selected) =>
                    {
                        var contextMenu = (AndroidContextMenu<MessagesContextMenu>)userData;
                        var item = contextMenu.GetItemAt(selected);

                        switch (item.Item)
                        {
                            case MessagesContextMenu.Copy:
                                {
                                    if (m_LayoutNodeValues.selectedItem == null)
                                        break;
                                    var value = (KeyValuePair<string, string>)m_LayoutNodeValues.selectedItem;
                                    EditorGUIUtility.systemCopyBuffer = $"{value.Key}={value.Value}";
                                }
                                break;
                            case MessagesContextMenu.CopyAll:
                                {
                                    if (m_LayoutNodesTreeView.selectedItem == null)
                                        break;
                                    var value = (AndroidLogcatQueryLayout.LayoutNode)m_LayoutNodesTreeView.selectedItem;
                                    EditorGUIUtility.systemCopyBuffer = string.Join("\n", value.Values.Select(x => $"{x.Key}={x.Value}"));
                                }
                                break;
                        }
                    });
                });
                m_LayoutNodeValues.columns[Name].makeCell = () => new Label();
                m_LayoutNodeValues.columns[Name].makeCell = () => new Label();
                m_LayoutNodeValues.columns[Name].bindCell = (v, i) =>
                {
                    var item = (KeyValuePair<string, string>)m_LayoutNodeValues.itemsSource[i];
                    ((Label)v).text = item.Key;
                };

                m_LayoutNodeValues.columns[Value].bindCell = (v, i) =>
                {
                    var item = (KeyValuePair<string, string>)m_LayoutNodeValues.itemsSource[i];
                    ((Label)v).text = item.Value;
                };
            }
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

        private void RefreshTreeView()
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

            m_LayoutNodesTreeView.ClearSelection();
            m_LayoutNodesTreeView.SetRootItems<AndroidLogcatQueryLayout.LayoutNode>(GetItems(m_QueryLayout.Nodes));
            m_LayoutNodesTreeView.RefreshItems();
            m_LayoutNodesTreeView.ExpandAll();
        }

        void OnQueryCaptureLayoutCompleted()
        {
            RefreshTreeView();
            if (m_DeviceSelection.SelectedDevice != null)
                m_CacheDisplaySize = m_DeviceSelection.SelectedDevice.QueryDisplaySize();
            else
                m_CacheDisplaySize = Vector2.zero;
        }

        void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Label(GUIContent.none, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));
            EditorGUI.EndDisabledGroup();
            m_DeviceSelection.DoGUI();
            EditorGUI.BeginDisabledGroup(m_DeviceSelection.SelectedDevice == null);
            if (GUILayout.Button(Styles.QueryUIHierarchy, AndroidLogcatStyles.toolbarButton))
            {
                m_CaptureScreenshot.QueueScreenCapture(m_DeviceSelection.SelectedDevice, Repaint);
                m_QueryLayout.ClearNodes();
                RefreshTreeView();
                m_QueryLayout.QueueCaptureLayout(m_DeviceSelection.SelectedDevice, OnQueryCaptureLayoutCompleted);
            }
            EditorGUI.EndDisabledGroup();
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

            if (m_SelectedNode == null || m_DeviceSelection.SelectedDevice == null)
                return;
            var bounds = m_SelectedNode.Bounds;
            rc = m_CaptureScreenshot.ScreenshotDrawingRect;
            rc = new Rect(
                (bounds.x / m_CacheDisplaySize.x) * rc.width + rc.x,
                (bounds.y / m_CacheDisplaySize.y) * rc.height + rc.y,
                Mathf.Ceil((bounds.width / m_CacheDisplaySize.x) * rc.width),
                Mathf.Ceil((bounds.height / m_CacheDisplaySize.y) * rc.height));

            AndroidLogcatUtilities.DrawRectangle(rc, 2, Color.red);
        }
    }
}
