#if PLATFORM_ANDROID
using UnityEngine;
using UnityEditor;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatIPWindow : EditorWindow
    {
        private ADB m_Adb = null;
        internal string m_IpString;
        internal bool m_DidFocus = false;

        private const string kIpTextFieldId = "IpTextField";
        private const string kAndroidLogcatLastIp = "AndroidLogcatLastIp";

        public static void Show(ADB adb, Rect screenRect)
        {
            var rect = new Rect(screenRect.x, screenRect.yMax, 300, 50);
            AndroidLogcatIPWindow win = EditorWindow.GetWindowWithRect<AndroidLogcatIPWindow>(rect, true, "Enter Device IP");
            win.m_Adb = adb;
            win.position = rect;
        }

        void OnEnable()
        {
            m_IpString = EditorPrefs.GetString(kAndroidLogcatLastIp, "");
        }

        void OnGUI()
        {
            Event evt = Event.current;
            bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName(kIpTextFieldId);

            EditorGUILayout.BeginVertical();
            {
                GUILayout.Space(5);
                m_IpString = EditorGUILayout.TextField(m_IpString);

                if (!m_DidFocus)
                {
                    m_DidFocus = true;
                    EditorGUI.FocusTextInControl(kIpTextFieldId);
                }

                GUI.enabled = !string.IsNullOrEmpty(m_IpString);
                if (GUILayout.Button("Connect") || hitEnter)
                {
                    Close();
                    EditorPrefs.SetString(kAndroidLogcatLastIp, m_IpString);
                    AndroidLogcatUtilities.ConnectDevice(m_Adb, m_IpString);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
