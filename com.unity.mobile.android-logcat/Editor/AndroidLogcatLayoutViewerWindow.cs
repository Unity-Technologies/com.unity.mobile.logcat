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
        TextField m_DisplaySizeTextField;
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
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, null);
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
            m_DisplaySizeTextField = r.Q<TextField>("DisplaySize");
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
                m_LayoutNodeValues.columns[Name].makeCell = () =>
                {
                    var l = new Label();
                    l.style.marginLeft = 5;
                    return l;
                };
                m_LayoutNodeValues.columns[Value].makeCell = () => new Label();
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

        void RefreshDisplaySize()
        {
            RefreshTreeView();
            if (m_DeviceSelection.SelectedDevice != null)
                m_CacheDisplaySize = m_DeviceSelection.SelectedDevice.QueryDisplaySize();
            else
                m_CacheDisplaySize = Vector2.zero;

            m_DisplaySizeTextField.value = $"{(int)m_CacheDisplaySize.x},{(int)m_CacheDisplaySize.y}";
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
                m_QueryLayout.QueueCaptureLayout(m_DeviceSelection.SelectedDevice, RefreshTreeView);
                RefreshDisplaySize();
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

        /// <summary>
        /// Performs recursive picking of elements.
        /// Since elments can overlap, we prefer elements with higher depth in the hierarchy
        /// </summary>
        void DoNodeRecursivePicking(IReadOnlyList<AndroidLogcatQueryLayout.LayoutNode> nodes, Vector2 mousePosition, int depth, ref KeyValuePair<int, int> result)
        {
            foreach (var n in nodes)
            {
                DoNodeRecursivePicking(n.Childs, mousePosition, depth + 1, ref result);

                // No point in check, if result's depth is higher than this node depth
                if (result.Key >= 0 && result.Value > depth)
                    continue;
                var rc = n.BoundsToScreen(m_CacheDisplaySize, m_CaptureScreenshot.ScreenshotDrawingRect);
                if (rc.Contains(mousePosition))
                    result = new KeyValuePair<int, int>(n.Id, depth);
            }
        }

        void DoNodePicking()
        {
            var e = Event.current;
            if (m_QueryLayout.Nodes.Count > 0 && e.type == EventType.MouseDown)
            {
                var r = new KeyValuePair<int, int>(-1, -1);
                DoNodeRecursivePicking(m_QueryLayout.Nodes, e.mousePosition, 0, ref r);

                if (r.Key >= 0)
                    m_LayoutNodesTreeView.SetSelection(r.Key);
            }
        }

        void DoLayoutImage()
        {
            var rc = GUILayoutUtility.GetRect(0, Screen.width, 0, Screen.height);
            m_CaptureScreenshot.DoGUI(rc);

            DoNodePicking();

            if (m_SelectedNode == null)
                return;
            rc = m_SelectedNode.BoundsToScreen(m_CacheDisplaySize, m_CaptureScreenshot.ScreenshotDrawingRect);

            AndroidLogcatUtilities.DrawRectangle(rc, 3, Color.black);
            AndroidLogcatUtilities.DrawRectangle(rc, 2, Color.red);
        }
    }
}
