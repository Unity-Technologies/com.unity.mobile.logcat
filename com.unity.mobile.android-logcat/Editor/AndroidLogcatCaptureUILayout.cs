using System;
using UnityEngine;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor.IMGUI.Controls;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCaptureUILayout
    {
        class SimpleTreeView : TreeView
        {
            List<LayoutNode> m_Nodes;

            public SimpleTreeView(TreeViewState treeViewState, List<LayoutNode> nodes)
                : base(treeViewState)
            {
                m_Nodes = nodes;
                Reload();
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
                var allItems = new List<TreeViewItem>();
                var currentId = 1;
                var currentDepth = 0;
                foreach (var n in m_Nodes)
                {
                    allItems.Add(new TreeViewItem
                    {
                        id = currentId++,
                        depth = currentDepth,
                        displayName = n.className
                    });
                }

                SetupParentsAndChildrenFromDepths(root, allItems);
                return root;
            }
        }

        internal class LayoutNode
        {
            internal string className;
            internal Rect bounds;
            internal List<LayoutNode> childs;

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
                        // Srip UI hierchary dumped to: /dev/tty at the end
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

        internal AndroidLogcatCaptureUILayout(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;

            m_Nodes = new List<LayoutNode>();
            m_TreeViewState = new TreeViewState();
            m_TreeView = new SimpleTreeView(m_TreeViewState, m_Nodes);
        }

        private void Integrate(IAndroidLogcatTaskResult result)
        {
            var r = (AndroidLogcatCaptureUILayoutResult)result;
            m_Nodes.Clear();

            try
            {
                try
                {
                    var doc = XDocument.Parse(r.rawLayout);
                    var xmlNodes = doc.Root.Elements("node");
                    ConstructNodes(m_Nodes, xmlNodes);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to create layout Doc", ex);
                }
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
            }

            m_TreeView.Reload();
            r.onCompleted();
        }

        private void ConstructNodes(List<LayoutNode> nodes, IEnumerable<XElement> nodeList)
        {
            foreach (var xNode in nodeList)
            {
                var node = new LayoutNode();
                nodes.Add(node);
                node.className = xNode.Attribute("class").Value;
            }
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
            m_TreeView.OnGUI(rc);
        }
    }
}
