using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using static Unity.Android.Logcat.AndroidLogcatCaptureUILayout;
using System.Xml.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatQueryLayout
    {
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

        internal class QueryLayoutInput : IAndroidLogcatTaskInput
        {
            internal AndroidBridge.ADB adb;
            internal string deviceId;
            internal Action onCompleted;
        }

        internal class QueryLayoutResult : IAndroidLogcatTaskResult
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

        internal AndroidLogcatQueryLayout(AndroidLogcatRuntimeBase runtime)
        {
            m_Runtime = runtime;
        }

        internal void QueueCaptureLayout(IAndroidLogcatDevice device, Action onCompleted)
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
                Execute,
                Integrate,
                false);
        }

        private static IAndroidLogcatTaskResult Execute(IAndroidLogcatTaskInput input)
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
                    //ConstructNodes(m_Nodes, xmlNodes);
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

            r.onCompleted();
        }

    }
}
