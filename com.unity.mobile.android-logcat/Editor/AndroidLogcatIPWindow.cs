#if PLATFORM_ANDROID
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatIPWindow : EditorWindow
    {
        private IAndroidLogcatRuntime m_Runtime;
        private ADB m_Adb = null;
        private List<string> m_ConnectedDevices;
        private string[] m_ConnectedDeviceDetails;
        internal string m_IpString;
        internal string m_PortString;
        private Vector2 m_DevicesScrollPosition = Vector2.zero;

        private const string kAndroidLogcatLastIp = "AndroidLogcatLastIp";
        private const string kAndroidLogcatLastPort = "AndroidLogcatLastPort";

        public static void Show(IAndroidLogcatRuntime runtime, ADB adb, List<string> connectedDevices, string[] details, Rect screenRect)
        {
            AndroidLogcatIPWindow win = EditorWindow.GetWindow<AndroidLogcatIPWindow>(true, "Enter Device IP");
            win.m_Runtime = runtime;
            win.m_Adb = adb;
            win.m_ConnectedDevices = connectedDevices;
            win.m_ConnectedDeviceDetails = details;
            win.position = new Rect(screenRect.x, screenRect.y, 600, 500);
        }

        void OnEnable()
        {
            m_IpString = EditorPrefs.GetString(kAndroidLogcatLastIp, "");
            m_PortString = EditorPrefs.GetString(kAndroidLogcatLastPort, "5555");

            // Disable progress bar just in case, if we have a stale process hanging where we peform adb connect
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Connect to the device by ip address.
        /// Please refer to https://developer.android.com/studio/command-line/adb#wireless for details.
        /// </summary>
        /// <param name="ip"> The ip address of the device that needs to be connected. Port can be included like 'device_ip_address:port'. Both IPV4 and IPV6 are supported. </param>
        public  void ConnectDevice(string ip, string port)
        {
            EditorUtility.DisplayProgressBar("Connecting", "Connecting to " + ip + ":" + port, 0.0f);
            m_Runtime.Dispatcher.Schedule(new AndroidLogcatConnectToDeviceInput() { adb = m_Adb, ip = ip, port = port}, AndroidLogcatConnectToDeviceTask.Execute, IntegrateConnectToDevice, false);
        }

        private static void IntegrateConnectToDevice(IAndroidLogcatTaskResult result)
        {
            var r = (AndroidLogcatConnectToDeviceResult)result;
            AndroidLogcatInternalLog.Log(r.message);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(r.success ? "Success" : "Failure", r.message, "Ok");
        }

        string QueryIP(string deviceId)
        {
            var result = m_Adb.Run(new[] { "-s", deviceId, "shell", "ip", "route"}, "Failed to query ip");
            var i = result.IndexOf("src ");
            if (i > 0)
                result = result.Substring(i + 4).Trim(new[] {' ', '\r', '\n'});
            return string.IsNullOrEmpty(result) ? "Failed to get IP address" : result;
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("Available devices:", EditorStyles.boldLabel);
                m_DevicesScrollPosition = EditorGUILayout.BeginScrollView(m_DevicesScrollPosition);
                for (int i = 0; i < m_ConnectedDevices.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var name = i < m_ConnectedDeviceDetails.Length && !string.IsNullOrEmpty(m_ConnectedDeviceDetails[i])
                        ? m_ConnectedDeviceDetails[i]
                        : m_ConnectedDevices[i];
                    var labelRect = GUILayoutUtility.GetRect(new GUIContent(name), EditorStyles.label);
                    var rc = GUILayoutUtility.GetLastRect();
                    GUI.Box(rc, "");
                    EditorGUI.LabelField(labelRect, name, EditorStyles.label);
                    if (GUILayout.Button(" Query IP ", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        m_IpString = QueryIP(m_ConnectedDevices[i]);
                        GUIUtility.keyboardControl = 0;
                        GUIUtility.hotControl = 0;
                        Repaint();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("IP", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Port", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                m_IpString = EditorGUILayout.TextField(m_IpString);
                m_PortString = EditorGUILayout.TextField(m_PortString, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                GUI.enabled = !string.IsNullOrEmpty(m_IpString);
                if (GUILayout.Button("Connect"))
                {
                    Close();
                    EditorPrefs.SetString(kAndroidLogcatLastIp, m_IpString);
                    EditorPrefs.SetString(kAndroidLogcatLastPort, m_PortString);
                    ConnectDevice(m_IpString, m_PortString);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
