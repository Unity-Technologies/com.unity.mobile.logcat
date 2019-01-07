#if PLATFORM_ANDROID
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatIPWindow : EditorWindow
    {
        private AndroidLogcatConsoleWindow m_AndroidLogcatConsoleWindow = null;
        internal string m_IPString;
        internal bool m_DidFocus = false;

        private const string kIPTextFieldId = "IPTextField";
        private const string kAndroidLogcatLastIP = "AndroidLogcatLastIP";

        public static void Show(AndroidLogcatConsoleWindow window, Rect screenRect)
        {
            var rect = new Rect(screenRect.x, screenRect.yMax, 300, 50);
            AndroidLogcatIPWindow win = EditorWindow.GetWindowWithRect<AndroidLogcatIPWindow>(rect, true, "Enter Device IP");
            win.m_AndroidLogcatConsoleWindow = window;
            win.position = rect;
        }

        void OnEnable()
        {
            m_IPString = EditorPrefs.GetString(kAndroidLogcatLastIP, "");
        }

        void OnGUI()
        {
            Event evt = Event.current;
            bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName(kIPTextFieldId);

            EditorGUILayout.BeginVertical();
            {
                GUILayout.Space(5);
                m_IPString = EditorGUILayout.TextField(m_IPString);

                if (!m_DidFocus)
                {
                    m_DidFocus = true;
                    EditorGUI.FocusTextInControl(kIPTextFieldId);
                }

                GUI.enabled = !string.IsNullOrEmpty(m_IPString);
                if (GUILayout.Button("Connect") || hitEnter)
                {
                    Close();
                    EditorPrefs.SetString(kAndroidLogcatLastIP, m_IPString);
                    m_AndroidLogcatConsoleWindow.ConnectDeviceByIPAddress(m_IPString);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
