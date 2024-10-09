using System;
using UnityEngine;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.IO;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatQueryLayout
    {
        static Regex BoundsRegex = new Regex(@"\[(?<x1>\d+),(?<y1>\d+)\]\[(?<x2>\d+),(?<y2>\d+)\]");

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
            internal string ResourceId { get; }
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

            internal LayoutNode(int id, string className, string resourceId, Rect bounds)
            {
                this.Id = id;
                this.ClassName = className;
                this.ResourceId = resourceId;
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
            internal IAndroidLogcatDevice device;
            internal Action onCompleted;
        }

        private class QueryLayoutResult : IAndroidLogcatTaskResult
        {
            internal string rawLayout;
            internal IAndroidLogcatDevice device;
            internal Action onCompleted;

            internal QueryLayoutResult(string layout, IAndroidLogcatDevice device, Action onCompleted)
            {
                this.rawLayout = layout;
                this.device = device;
                this.onCompleted = onCompleted;
            }
        }

        internal class LoadResult
        {
            internal string RawLayout { get; private set; }
            internal AndroidScreenRotation Rotation { get; private set; }
            internal Vector2 DisplaySize { get; private set; }
            internal Vector2? OverridenDisplaySize { get; private set; }
            internal Vector2 DisplaySizeRotated
            {
                get
                {
                    var size = OverridenDisplaySize.HasValue ? OverridenDisplaySize.Value : DisplaySize;

                    if (Rotation == AndroidScreenRotation.Landscape || Rotation == AndroidScreenRotation.LandscapeReversed)
                        return new Vector2(size.y, size.x);
                    return size;
                }
            }

            internal LoadResult()
            {
                Clear();
            }

            internal LoadResult(string rawLayout, AndroidScreenRotation rotation, Vector2 displaySize, Vector2? overridenDisplaySize)
            {
                RawLayout = rawLayout;
                Rotation = rotation;
                DisplaySize = displaySize;
                OverridenDisplaySize = overridenDisplaySize;
            }

            private void Clear()
            {
                RawLayout = string.Empty;
                Rotation = 0;
                DisplaySize = Vector2.zero;
                OverridenDisplaySize = null;
            }
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private List<LayoutNode> m_Nodes;
        private int m_QueryCount;
        internal bool IsQuerying => m_QueryCount > 0;

        internal IReadOnlyList<LayoutNode> Nodes => m_Nodes;

        internal LoadResult LastLoaded { get; private set; }

        internal AndroidLogcatQueryLayout(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
            LastLoaded = new LoadResult();
            m_Nodes = new List<LayoutNode>();
        }

        internal void Clear()
        {
            LastLoaded = new LoadResult();
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
                    device = device,
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

                var cmd = $"-s {workInput.device.Id} exec-out uiautomator dump /dev/tty";
                var outputMsg = adb.Run(new[] { cmd }, "Unable to get UI layout");
                AndroidLogcatInternalLog.Log($"adb {cmd}");

                if (!string.IsNullOrEmpty(outputMsg))
                {
                    var stripped = ExtractLayoutXmlFromOutput(outputMsg);
                    if (string.IsNullOrEmpty(stripped))
                        AndroidLogcatInternalLog.Log($"No layout?\n{outputMsg}");
                    outputMsg = stripped;
                }
                return new QueryLayoutResult(outputMsg, workInput.device, workInput.onCompleted);

            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                return new QueryLayoutResult(string.Empty, null, workInput.onCompleted);
            }
        }

        internal static string ExtractLayoutXmlFromOutput(string contents)
        {
            if (string.IsNullOrEmpty(contents))
                return contents;
            var startTag = "<?xml";
            var endTag = "</hierarchy>";

            var startIndex = contents.IndexOf(startTag, StringComparison.InvariantCultureIgnoreCase);
            var endIndex = contents.IndexOf(endTag, StringComparison.InvariantCultureIgnoreCase);

            if (startIndex == -1 || endIndex == -1 || startIndex > endIndex)
                return string.Empty;

            return contents.Substring(startIndex, endIndex + endTag.Length - startIndex);
        }

        private static string SafeAttributeValue(XElement element, string attributeName)
        {
            if (element == null)
                return string.Empty;
            var a = element.Attribute(attributeName);
            if (a == null)
                return string.Empty;
            return a.Value;
        }

        private static void ConstructNodes(List<LayoutNode> nodes, IEnumerable<XElement> nodeList, ref int id)
        {
            foreach (var xNode in nodeList)
            {
                var rc = Rect.zero;
                var b = BoundsRegex.Match(SafeAttributeValue(xNode, BoundsTag));
                if (b.Success)
                {
                    rc = new Rect(int.Parse(b.Groups["x1"].Value),
                        int.Parse(b.Groups["y1"].Value),
                        0, 0);
                    rc.width = int.Parse(b.Groups["x2"].Value) - rc.xMin;
                    rc.height = int.Parse(b.Groups["y2"].Value) - rc.yMin;
                }

                var node = new LayoutNode(id++,
                    xNode.Attribute(ClassTag).Value,
                    SafeAttributeValue(xNode, ResourceIdTag),
                    rc);
                foreach (var a in xNode.Attributes())
                {
                    node.Values[a.Name.ToString()] = a.Value;
                }

                nodes.Add(node);

                ConstructNodes(node.Childs, xNode.Elements(NodeTag), ref id);
            }
        }

        internal static void ParseNodes(List<LayoutNode> nodes, out AndroidScreenRotation rotation, string rawLayout)
        {
            rotation = 0;
            nodes.Clear();
            if (string.IsNullOrEmpty(rawLayout))
                return;
            var doc = XDocument.Parse(rawLayout);
            const string Rotation = "rotation";
            var attrRotation = doc.Root.Attribute(Rotation);
            if (attrRotation == null)
                throw new Exception($"Failed to find {Rotation} attribute.");
            rotation = (AndroidScreenRotation)int.Parse(attrRotation.Value);

            var id = 0;
            var rootNode = new LayoutNode(id++, doc.Root.Name.ToString(), string.Empty, Rect.zero);
            rootNode.Values[Rotation] = $"{rotation.ToString()}({(int)rotation})";
            nodes.Add(rootNode);

            var xmlNodes = doc.Root.Elements(NodeTag);
            ConstructNodes(rootNode.Childs, xmlNodes, ref id);
        }

        private void Integrate(IAndroidLogcatTaskResult result)
        {
            var r = (QueryLayoutResult)result;

            try
            {
                ParseNodes(m_Nodes, out var rotation, r.rawLayout);

                var displaySize = Vector2.zero;
                Vector2? overridenDisplaySize = null;
                if (r.device != null)
                    r.device.QueryDisplaySize(out displaySize, out overridenDisplaySize);

                LastLoaded = new LoadResult(r.rawLayout, (AndroidScreenRotation)rotation, displaySize, overridenDisplaySize);
                // If there were no nodes, create empty one
                if (m_Nodes.Count == 0)
                    m_Nodes.Add(new LayoutNode(0, "Empty", string.Empty, Rect.zero));
            }
            catch (Exception ex)
            {
                m_Nodes.Clear();

                var path = "<Not Saved>";
                if (!string.IsNullOrEmpty(r.rawLayout))
                {
                    path = Path.Combine("Temp", "failed_to_parse.txt");
                    File.WriteAllText(path, r.rawLayout);
                }

                var node = new LayoutNode(0, "Error while parsing layout", string.Empty, Rect.zero)
                {
                    Values =
                    {
                        ["save_location"] = path
                    }
                };
                m_Nodes.Add(node);
                AndroidLogcatInternalLog.Log($"Failed to parse layout (saved in '<project>/{path}'):\n{ex.Message}");
            }
            finally
            {
                m_QueryCount--;
            }

            r.onCompleted();
        }
    }
}
