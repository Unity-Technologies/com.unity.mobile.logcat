using System;
using UnityEngine;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatQueryLayout
    {
        Regex BoundsRegex = new Regex(@"\[(?<x1>\d+),(?<y1>\d+)\]\[(?<x2>\d+),(?<y2>\d+)\]");

        const string NodeTag = "node";
        const string ClassTag = "class";
        const string BoundsTag = "bounds";
        const string ResourceIdTag = "resource-id";
        const string TextTag = "text";

        internal class LayoutNode
        {
            // Ensures class/bounds/resource-id/text is at the top of the list
            class Comparer : IComparer<string>
            {
                readonly string[] Items = new[] { ClassTag, BoundsTag, ResourceIdTag, TextTag };

                public int Compare(string x, string y)
                {
                    var result = x.CompareTo(y);
                    if (result == 0)
                        return 0;
                    for (int i = 0; i < Items.Length; i++)
                    {
                        if (x.Equals(Items[i]))
                            return -i - 2;
                        if (y.Equals(Items[i]))
                            return i + 2;
                    }

                    return result;
                }
            }

            internal string ClassName { get; }
            internal Rect Bounds { get; }
            internal List<LayoutNode> Childs { get; } = new List<LayoutNode>();
            internal SortedDictionary<string, string> Values { get; } = new SortedDictionary<string, string>(new Comparer());
            internal int Id { get; }

            internal Rect BoundsToScreen(Vector2 deviceDisplaySize, Rect uiWindow)
            {
                return new Rect(
                    (Bounds.x / deviceDisplaySize.x) * uiWindow.width + uiWindow.x,
                    (Bounds.y / deviceDisplaySize.y) * uiWindow.height + uiWindow.y,
                    Mathf.Ceil((Bounds.width / deviceDisplaySize.x) * uiWindow.width),
                    Mathf.Ceil((Bounds.height / deviceDisplaySize.y) * uiWindow.height));
            }

            internal LayoutNode(int id, string className, Rect bounds)
            {
                this.Id = id;
                this.ClassName = className;
                this.Bounds = bounds;
            }

            public override string ToString()
            {
                return $"{ClassName} {Bounds} Id = {Id}";
            }
        }

        private class QueryLayoutInput : IAndroidLogcatTaskInput
        {
            internal AndroidBridge.ADB adb;
            internal string deviceId;
            internal Action onCompleted;
        }

        private class QueryLayoutResult : IAndroidLogcatTaskResult
        {
            internal string rawLayout;
            internal Action onCompleted;

            internal QueryLayoutResult(string layout, Action onCompleted)
            {
                this.onCompleted = onCompleted;
                this.rawLayout = layout;
            }
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private List<LayoutNode> m_Nodes;
        private int m_QueryCount;
        public bool IsQuerying => m_QueryCount > 0;

        internal IReadOnlyList<LayoutNode> Nodes => m_Nodes;

        internal AndroidLogcatQueryLayout(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            m_Nodes = new List<LayoutNode>();
        }

        internal void ClearNodes()
        {
            m_Nodes.Clear();
        }

        internal void QueueCaptureLayout(IAndroidLogcatDevice device, Action onCompleted)
        {
            if (device == null)
                return;

            m_QueryCount++;
            m_Runtime.Dispatcher.Schedule(
                new QueryLayoutInput()
                {
                    adb = m_Runtime.Tools.ADB,
                    deviceId = device.Id,
                    onCompleted = onCompleted
                },
                Execute,
                Integrate,
                false);
        }

        private static IAndroidLogcatTaskResult Execute(IAndroidLogcatTaskInput input)
        {
            var workInput = ((QueryLayoutInput)input);

            try
            {
                var adb = workInput.adb;

                if (adb == null)
                    throw new NullReferenceException("ADB interface has to be valid");

                var cmd = $"-s {workInput.deviceId} exec-out uiautomator dump /dev/tty";
                var outputMsg = adb.Run(new[] { cmd }, "Unable to get UI layout");
                AndroidLogcatInternalLog.Log($"adb {cmd}");

                if (!string.IsNullOrEmpty(outputMsg))
                {
                    // Srip UI hierchary dumped to: /dev/tty at the end
                    var endTag = "</hierarchy>";
                    var idx = outputMsg.IndexOf(endTag, StringComparison.InvariantCultureIgnoreCase);
                    if (idx > 0)
                        outputMsg = outputMsg.Substring(0, idx + endTag.Length);
                    else if (!outputMsg.StartsWith("<hierarchy"))
                    {
                        AndroidLogcatInternalLog.Log($"No layout?\n{outputMsg}");
                        outputMsg = string.Empty;
                    }
                }

                return new QueryLayoutResult(outputMsg, workInput.onCompleted);

            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                return new QueryLayoutResult(string.Empty, workInput.onCompleted);
            }
        }

        private void ConstructNodes(List<LayoutNode> nodes, IEnumerable<XElement> nodeList, ref int id)
        {
            foreach (var xNode in nodeList)
            {
                var rc = Rect.zero;
                var b = BoundsRegex.Match(xNode.Attribute(BoundsTag).Value);
                if (b.Success)
                {
                    rc = new Rect(int.Parse(b.Groups["x1"].Value),
                        int.Parse(b.Groups["y1"].Value),
                        0, 0);
                    rc.width = int.Parse(b.Groups["x2"].Value) - rc.xMin;
                    rc.height = int.Parse(b.Groups["y2"].Value) - rc.yMin;
                }
                var node = new LayoutNode(id++, xNode.Attribute(ClassTag).Value, rc);
                foreach (var a in xNode.Attributes())
                {
                    node.Values[a.Name.ToString()] = a.Value;
                }

                nodes.Add(node);

                ConstructNodes(node.Childs, xNode.Elements(NodeTag), ref id);
            }
        }

        private void Integrate(IAndroidLogcatTaskResult result)
        {
            var r = (QueryLayoutResult)result;
            m_Nodes.Clear();

            try
            {
                if (!string.IsNullOrEmpty(r.rawLayout))
                {
                    var doc = XDocument.Parse(r.rawLayout);
                    var xmlNodes = doc.Root.Elements(NodeTag);
                    var id = 0;
                    ConstructNodes(m_Nodes, xmlNodes, ref id);
                }

                // If there were no nodes, create empty one
                if (m_Nodes.Count == 0)
                    m_Nodes.Add(new LayoutNode(0, "Empty", Rect.zero));
            }
            catch (Exception ex)
            {
                m_Nodes.Clear();
                var node = new LayoutNode(0, "Error while parsing layout", Rect.zero)
                {
                    Values =
                    {
                        ["error"] = ex.Message,
                        ["rawLayout"] = r.rawLayout
                    }
                };
                m_Nodes.Add(node);
                AndroidLogcatInternalLog.Log($"Failed to create layout Doc:\n{ex.Message}");
            }
            finally
            {
                m_QueryCount--;
            }
            
            r.onCompleted();
        }

    }
}
