using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatLayoutViewerWindow : EditorWindow
    {
        internal static void ShowWindow()
        {
            var window = (AndroidLogcatLayoutViewerWindow)EditorWindow.GetWindow(typeof(AndroidLogcatLayoutViewerWindow));
            window.titleContent = new GUIContent("Layout Viewer");
            window.Show();
        }

#if UNITY_2022_3_OR_NEWER
        const string Name = nameof(Name);
        const string Value = nameof(Value);

        class Styles
        {
            public static GUIContent QueryUIHierarchy = new GUIContent("Query UI Layout");
            public static GUIContent SaveUIHierarchy = new GUIContent("Save Layout");
            public static GUIContent SaveScreenshot = new GUIContent("Save Screenshot");
        }

        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatDeviceSelection m_DeviceSelection;
        AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        AndroidLogcatQueryLayout m_QueryLayout;
        TreeView m_LayoutNodesTreeView;
        MultiColumnListView m_LayoutNodeValues;
        AndroidLogcatQueryLayout.LayoutNode m_SelectedNode;
        TextField m_DisplaySizeTextField;
        TextField m_OverridenDisplaySizeTextField;

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (rootVisualElement == null)
                throw new NullReferenceException("rooVisualElement is null");

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.Closing += OnDisable;
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, null, nameof(AndroidLogcatLayoutViewerWindow) + "_DeviceId");
            m_CaptureScreenshot = m_Runtime.CaptureScreenshot;
            m_QueryLayout = m_Runtime.QueryLayout;

            LoadUI();

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
        }

        private void LoadUI()
        {
            var r = rootVisualElement;
            /* Keep it for reference
            if (Unsupported.IsDeveloperMode())
                r.Insert(0, new IMGUIContainer(DoDebuggingGUI));
            //*/
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

                    var tooltip = string.Empty;
                    if (!string.IsNullOrEmpty(item.ResourceId))
                        tooltip = item.ResourceId + "\n";
                    tooltip += item.Bounds.ToString();
                    ((Label)v).tooltip = tooltip;
                };

                m_LayoutNodesTreeView.selectionChanged += (IEnumerable<object> objs) =>
                {
                    if (objs.Count() == 0 || objs.First() == null)
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

                    var value = item.Value;
                    if (value.Length > 50)
                        value = value.Substring(0, 50);
                    ((Label)v).text = value;
                };
            }

            m_DisplaySizeTextField = r.Q<TextField>("DisplaySize");
            m_OverridenDisplaySizeTextField = r.Q<TextField>("OverridenDisplaySize");
            UpdateDisplaySizeField();
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

        void UpdateDisplaySizeField()
        {
            var d = m_QueryLayout.LastLoaded.DisplaySize;
            m_DisplaySizeTextField.value = $"{(int)d.x},{(int)d.y}";

            var od = m_QueryLayout.LastLoaded.OverridenDisplaySize;
            if (od.HasValue)
            {
                m_OverridenDisplaySizeTextField.value = $"{(int)od.Value.x},{(int)od.Value.y}";
                m_OverridenDisplaySizeTextField.visible = true;
            }
            else
            {
                m_OverridenDisplaySizeTextField.value = "0,0";
                m_OverridenDisplaySizeTextField.visible = false;
            }
        }

        void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            EditorGUI.BeginDisabledGroup(true);
            AndroidLogcatUtilities.DrawProgressIcon(m_CaptureScreenshot.IsCapturing || m_QueryLayout.IsQuerying);
            EditorGUI.EndDisabledGroup();
            m_DeviceSelection.DoGUI();
            EditorGUI.BeginDisabledGroup(m_DeviceSelection.SelectedDevice == null);
            if (GUILayout.Button(Styles.QueryUIHierarchy, AndroidLogcatStyles.toolbarButton))
            {
                m_CaptureScreenshot.QueueScreenCapture(m_DeviceSelection.SelectedDevice, Repaint);
                m_QueryLayout.Clear();
                RefreshTreeView();
                m_QueryLayout.QueueCaptureLayout(m_DeviceSelection.SelectedDevice, () =>
                {
                    RefreshTreeView();
                    UpdateDisplaySizeField();
                });
            }
            DoScreenshotSaveAsGUI();
            DoLayoutSaveAsGUI();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private string ResolveDisplaySizeString()
        {
            var d = m_QueryLayout.LastLoaded.OverridenDisplaySize.HasValue ?
                m_QueryLayout.LastLoaded.OverridenDisplaySize.Value :
                m_QueryLayout.LastLoaded.DisplaySize;
            return $"{(int)d.x}x{(int)d.y}";
        }
        private void DoScreenshotSaveAsGUI()
        {
            var srcPath = m_CaptureScreenshot.GetImagePath(m_DeviceSelection.SelectedDevice);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(srcPath) || m_CaptureScreenshot.ImageTexture == null);
            if (GUILayout.Button(Styles.SaveScreenshot, AndroidLogcatStyles.toolbarButton))
            {
                var d = ResolveDisplaySizeString();
                var fileName = $"{Path.GetFileNameWithoutExtension(srcPath)}_{d}{m_CaptureScreenshot.GetImageExtension()}";
                var path = EditorUtility.SaveFilePanel(
                    "Save Screenshot",
                    m_Runtime.UserSettings.LayoutSettings.LastScreenshotSaveLocation,
                    fileName,
                    m_CaptureScreenshot.GetImageExtension().Substring(1));
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        m_Runtime.UserSettings.LayoutSettings.LastScreenshotSaveLocation = Path.GetFullPath(Path.GetDirectoryName(path));
                        File.Copy(srcPath, path, true);
                        GUIUtility.ExitGUI();
                    }
                    catch (ExitGUIException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to save to '{path}'\n:'{ex.Message}'.");
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DoLayoutSaveAsGUI()
        {
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(m_QueryLayout.LastLoaded.RawLayout));
            if (GUILayout.Button(Styles.SaveUIHierarchy, AndroidLogcatStyles.toolbarButton))
            {
                var d = ResolveDisplaySizeString();
                var fileName = $"layout_{m_DeviceSelection.SelectedDevice.Id}_{d}.xml";
                var path = EditorUtility.SaveFilePanel(
                    "Save Layout",
                    m_Runtime.UserSettings.LayoutSettings.LastLayoutSaveLocation,
                    fileName,
                    "xml");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        m_Runtime.UserSettings.LayoutSettings.LastLayoutSaveLocation = Path.GetFullPath(Path.GetDirectoryName(path));
                        File.WriteAllText(path, m_QueryLayout.LastLoaded.RawLayout);
                        GUIUtility.ExitGUI();
                    }
                    catch (ExitGUIException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to save to '{path}'\n:'{ex.Message}'.");
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
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
        void DoNodeRecursivePicking(Vector2 displaySize, IReadOnlyList<AndroidLogcatQueryLayout.LayoutNode> nodes, Vector2 mousePosition, int depth, ref KeyValuePair<int, int> result)
        {
            foreach (var n in nodes)
            {
                DoNodeRecursivePicking(displaySize, n.Childs, mousePosition, depth + 1, ref result);

                // No point in check, if result's depth is higher than this node depth
                if (result.Key >= 0 && result.Value > depth)
                    continue;
                var rc = n.BoundsToScreen(displaySize, m_CaptureScreenshot.ScreenshotDrawingRect);
                if (rc.Contains(mousePosition))
                    result = new KeyValuePair<int, int>(n.Id, depth);
            }
        }

        void DoNodePicking(Vector2 displaySize)
        {
            var e = Event.current;
            if (m_QueryLayout.Nodes.Count > 0 && e.type == EventType.MouseDown)
            {
                var r = new KeyValuePair<int, int>(-1, -1);
                DoNodeRecursivePicking(displaySize, m_QueryLayout.Nodes, e.mousePosition, 0, ref r);

                if (r.Key >= 0)
                {
                    m_LayoutNodesTreeView.ExpandAll();
                    m_LayoutNodesTreeView.ScrollToItem(r.Key);
                    m_LayoutNodesTreeView.SetSelection(r.Key);
                }
            }
        }

        void DoLayoutImage()
        {
            var rc = GUILayoutUtility.GetRect(0, Screen.width, 0, Screen.height);
            if (!m_CaptureScreenshot.DoGUI(rc))
            {
                EditorGUI.HelpBox(rc, $"No layout to show, click {Styles.QueryUIHierarchy.text} button.", MessageType.Info);
                return;
            }

            var displaySize = m_QueryLayout.LastLoaded.DisplaySizeRotated;

            DoNodePicking(displaySize);

            if (m_SelectedNode == null)
                return;
            rc = m_SelectedNode.BoundsToScreen(displaySize, m_CaptureScreenshot.ScreenshotDrawingRect);

            AndroidLogcatUtilities.DrawRectangle(rc, 3, Color.black);
            AndroidLogcatUtilities.DrawRectangle(rc, 2, Color.red);
        }
#else
        public void OnGUI()
        {
            EditorGUILayout.HelpBox("Layout Viewer requires Unity 2022.3 or higher.", MessageType.Error);
        }
#endif
    }
}
