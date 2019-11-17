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
        private Rect m_DeviceScrollRect = new Rect();

        private const string kAndroidLogcatLastIp = "AndroidLogcatLastIp";
        private const string kAndroidLogcatLastPort = "AndroidLogcatLastPort";

        private GUIContent kConnect = new GUIContent(L10n.Tr("Connect"), L10n.Tr("Sets the target device to listen for a TCP/IP connection on port 5555 and connects to it via IP address."));

        public static void Show(IAndroidLogcatRuntime runtime, ADB adb, List<string> connectedDevices, string[] details, Rect screenRect)
        {
            AndroidLogcatIPWindow win = EditorWindow.GetWindow<AndroidLogcatIPWindow>(true, "Enter Device IP");
            win.m_ConnectedDevices = connectedDevices;
            win.m_ConnectedDeviceDetails = details;
            win.position = new Rect(screenRect.x, screenRect.y, 600, 200);
        }

        void OnEnable()
        {
            if (m_Adb == null)
                m_Adb = ADB.GetInstance();
            if (m_Runtime == null)
                m_Runtime = AndroidLogcatManager.instance.Runtime;
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

        public void SetTCPIPAndConnectDevice(string deviceId, string ip, string port)
        {
            EditorUtility.DisplayProgressBar("Connecting",
                string.Join("\n", new string[]
                {
                    "Set listening port to " + port + ". Connecting to " + ip + ":" + port,
                }), 0.0f);
            m_Runtime.Dispatcher.Schedule(new AndroidLogcatConnectToDeviceInput() { adb = m_Adb, ip = ip, port = port, deviceId = deviceId, setListeningPort = true }, AndroidLogcatConnectToDeviceTask.Execute, IntegrateConnectToDevice, false);
        }

        private static void IntegrateConnectToDevice(IAndroidLogcatTaskResult result)
        {
            var r = (AndroidLogcatConnectToDeviceResult)result;
            AndroidLogcatInternalLog.Log(r.message);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(r.success ? "Success" : "Failure", r.message, "Ok");
        }

        string CopyIP(string deviceId)
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
                GUI.Box(m_DeviceScrollRect, GUIContent.none, EditorStyles.helpBox);
                m_DevicesScrollPosition = EditorGUILayout.BeginScrollView(m_DevicesScrollPosition);
                for (int i = 0; i < m_ConnectedDevices.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var name = i < m_ConnectedDeviceDetails.Length && !string.IsNullOrEmpty(m_ConnectedDeviceDetails[i])
                        ? m_ConnectedDeviceDetails[i]
                        : m_ConnectedDevices[i];


                    EditorGUILayout.LabelField(name, EditorStyles.label);

                    if (GUILayout.Button(" Copy IP ", GUILayout.ExpandWidth(false)))
                    {
                        m_IpString = CopyIP(m_ConnectedDevices[i]);
                        EditorGUIUtility.systemCopyBuffer = m_IpString;
                        GUIUtility.keyboardControl = 0;
                        GUIUtility.hotControl = 0;
                        Repaint();
                    }
                    if (GUILayout.Button(kConnect, GUILayout.ExpandWidth(false)))
                    {
                        SetTCPIPAndConnectDevice(m_ConnectedDevices[i], CopyIP(m_ConnectedDevices[i]), "5555");
                        GUIUtility.keyboardControl = 0;
                        GUIUtility.hotControl = 0;
                        Repaint();
                    }

                    var rc = GUILayoutUtility.GetLastRect();
                    var orgColor = GUI.color;
                    GUI.color = Color.black;
                    if (Event.current.type == EventType.Repaint)
                        GUI.DrawTexture(new Rect(0, rc.y + rc.height, m_DeviceScrollRect.width, 1), EditorGUIUtility.whiteTexture);
                    GUI.color = orgColor;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                if (Event.current.type == EventType.Repaint)
                    m_DeviceScrollRect = GUILayoutUtility.GetLastRect();
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
