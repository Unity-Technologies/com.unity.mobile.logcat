using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCaptureUILayout
    {
        class SimpleTreeView : TreeView
        {
            List<LayoutNode> m_Nodes;
            List<TreeViewItem> m_AllItems;
            private List<LayoutNode> m_FlatNodes;

            public LayoutNode GetSelectedNode()
            {
                var sels = GetSelection();
                if (sels.Count == 0)
                    return null;
                
                var modifiedId = sels[0] - 1;
                if (modifiedId < 0 || modifiedId >= m_FlatNodes.Count)
                    return null;
                return m_FlatNodes[modifiedId];
            }
            
            public SimpleTreeView(TreeViewState treeViewState, List<LayoutNode> nodes)
                : base(treeViewState)
            {
                m_Nodes = nodes;
                m_AllItems = new List<TreeViewItem>();
                m_FlatNodes = new List<LayoutNode>();
                Reload();
            }

            private void BuildTreeRecursively(List<TreeViewItem> allItems, List<LayoutNode> nodes, List<LayoutNode> flatNodes, ref int id, int depth)
            {
                foreach (var node in nodes)
                {
                    allItems.Add(new TreeViewItem
                    {
                        id = id++,
                        depth = depth,
                        displayName = node.className
                    });
                    flatNodes.Add(node);
                }

                foreach (var node in nodes)
                {
                    BuildTreeRecursively(allItems, node.Childs, flatNodes, ref id, depth + 1);
                }
            }
            
            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
                m_AllItems.Clear();
                m_FlatNodes.Clear();
                var currentId = 1;
                
                BuildTreeRecursively(m_AllItems, m_Nodes, m_FlatNodes, ref currentId, 0);

                SetupParentsAndChildrenFromDepths(root, m_AllItems);
                return root;
            }
        }

        internal class LayoutNode
        {
            internal string className;
            internal Rect bounds;
            internal List<LayoutNode> Childs { get; } = new List<LayoutNode>();
            internal Dictionary<string, string> Values { get; } = new Dictionary<string, string>();
            
            public override string ToString()
            {
                return $"{className} {bounds}";
            }
        }

        internal class AndroidLogcatCaptureUILayoutInput : IAndroidLogcatTaskInput
        {
            internal AndroidBridge.ADB adb;
            internal string deviceId;
            internal Action onCompleted;
        }

        internal class AndroidLogcatCaptureUILayoutResult : IAndroidLogcatTaskResult
        {
            internal string rawLayout;
            internal Action onCompleted;

            internal AndroidLogcatCaptureUILayoutResult(string layout, Action onCompleted)
            {
                this.onCompleted = onCompleted;
                this.rawLayout = layout;
            }
        }

        internal class AndroidLogcatCaptureUILayoutTask
        {
            internal static IAndroidLogcatTaskResult Execute(IAndroidLogcatTaskInput input)
            {
                var workInput = ((AndroidLogcatCaptureUILayoutInput)input);

                try
                {

                    var adb = workInput.adb;

                    if (adb == null)
                        throw new NullReferenceException("ADB interface has to be valid");

                    var cmd = $"-s {workInput.deviceId} exec-out uiautomator dump /dev/tty";
                    var outputMsg = adb.Run(new[] { cmd }, "Unable to get UI layout");


                    if (!string.IsNullOrEmpty(outputMsg))
                    {
                        // Strip UI hierchary dumped to: /dev/tty at the end
                        var endTag = "</hierarchy>";
                        var idx = outputMsg.IndexOf(endTag, StringComparison.InvariantCultureIgnoreCase);
                        if (idx > 0)
                            outputMsg = outputMsg.Substring(0, idx + endTag.Length);
                    }

                    return new AndroidLogcatCaptureUILayoutResult(outputMsg, workInput.onCompleted);

                }
                catch (Exception ex)
                {
                    AndroidLogcatInternalLog.Log(ex.Message);
                    return new AndroidLogcatCaptureUILayoutResult(string.Empty, workInput.onCompleted);
                }
            }
        }


        AndroidLogcatRuntimeBase m_Runtime;
        List<LayoutNode> m_Nodes;
        TreeViewState m_TreeViewState;
        SimpleTreeView m_TreeView;
        AndroidLogcatFastListView m_FastListView;
        LayoutNode m_SelectedNode;

        internal AndroidLogcatCaptureUILayout(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;

            m_Nodes = new List<LayoutNode>();
            m_TreeViewState = new TreeViewState();
            m_TreeView = new SimpleTreeView(m_TreeViewState, m_Nodes);
            m_FastListView = new AndroidLogcatFastListView(() => AndroidLogcatStyles.internalLogStyle, 1000);
        }

        private void Integrate(IAndroidLogcatTaskResult result)
        {
            var r = (AndroidLogcatCaptureUILayoutResult)result;
            m_Nodes.Clear();
            
            try
            {
                var doc = XDocument.Parse(r.rawLayout);
                AndroidLogcatInternalLog.Log(doc.ToString());
                var xmlNodes = doc.Root.Elements("node");
                ConstructNodes(m_Nodes, xmlNodes);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
            }

            
            m_TreeView.Reload();
            m_TreeView.ExpandAll();
            r.onCompleted();
        }

        private void ConstructNodes(List<LayoutNode> nodes, IEnumerable<XElement> nodeList)
        {
            foreach (var xNode in nodeList)
            {
                var node = new LayoutNode();
                nodes.Add(node);
                node.className = xNode.Attribute("class").Value;
                foreach (var a in xNode.Attributes())
                {
                    node.Values[a.Name.ToString()] = a.Value;
                }

                ConstructNodes(node.Childs, xNode.Elements("node"));
            }
        }

        private void RefreshAttributes()
        {
            m_FastListView.ClearEntries();
            
            var selection = m_TreeView.GetSelectedNode();
            if (selection == null)
                return;


            m_FastListView.AddEntries(selection.Values.Select(p => $"{p.Key}: {p.Value}").ToArray());
        }

        public void QueueCapture(IAndroidLogcatDevice device, Action onCompleted)
        {
            if (device == null)
                return;

            m_Runtime.Dispatcher.Schedule(
                new AndroidLogcatCaptureUILayoutInput()
                {
                    adb = m_Runtime.Tools.ADB,
                    deviceId = device.Id,
                    onCompleted = onCompleted
                },
                AndroidLogcatCaptureUILayoutTask.Execute,
                Integrate,
                false);
        }

        public void DoGUI(Rect rc)
        {
            var rc1 = new Rect(rc.x, rc.y, rc.width, rc.height * 0.5f);
            var rc2 = new Rect(rc.x, rc.y + rc.height * 0.5f, rc.width, rc.height * 0.5f);
            m_TreeView.OnGUI(rc1);
            if (m_SelectedNode != m_TreeView.GetSelectedNode())
            {
                m_SelectedNode = m_TreeView.GetSelectedNode();
                RefreshAttributes();
            }

            GUILayout.BeginArea(rc2);
            m_FastListView.OnGUI();
            GUILayout.EndArea();
        }
    }
}
