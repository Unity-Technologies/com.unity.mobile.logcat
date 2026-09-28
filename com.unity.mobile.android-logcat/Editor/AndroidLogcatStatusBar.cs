using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatStatusBar
    {
        static class Styles
        {
            // "Settings" is the flat cog and has a per skin variant, which IconContent
            // picks. "_Popup" is the older one and carries a pale plate of its own,
            // which reads as a box on the bar.
            static readonly GUIContent kCog = EditorGUIUtility.IconContent("Settings");

            internal static readonly GUIContent Settings = kCog != null && kCog.image != null
                ? new GUIContent(kCog.image, "Open Android Logcat settings")
                : new GUIContent("Settings", "Open Android Logcat settings");
        }

        // Room for the cog at the right end, and a margin at either end of the bar.
        const float kIconSize = 16.0f;
        const float kMargin = 10.0f;

        public string Message { set; get; }

        public bool Connected { set; get; }

        /// <summary>
        /// Whether the bar leads with the connection state. A window that has nothing
        /// to connect to - the Screen Capture window - shows only its message.
        /// </summary>
        public bool ShowConnection { set; get; }

        public AndroidLogcatStatusBar()
        {
            Message = String.Empty;
            ShowConnection = true;
        }

        public void DoGUI()
        {
            var rc = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.statusLabel, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                AndroidLogcatStyles.statusBarBackground.Draw(rc, false, true, false, false);
            }
            var button = new Rect(rc.xMax - kIconSize - kMargin,
                rc.y + (rc.height - kIconSize) * 0.5f, kIconSize, kIconSize);
            if (GUI.Button(button, Styles.Settings, EditorStyles.iconButton))
                SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath);

            rc.x += kMargin;
            // The message stops short of the cog rather than running under it.
            rc.width -= kMargin * 2 + kIconSize;
            var msg = string.Empty;
            if (ShowConnection)
                msg = Connected ? "<color=#00FF00FF><b>Connected</b></color>" : "<color=#FF0000FF><b>Disconnected</b></color>";

            if (!string.IsNullOrEmpty(Message))
            {
                if (msg.Length > 0)
                    msg += " : ";
                msg += Message;
            }

            GUI.Label(rc, msg, AndroidLogcatStyles.statusLabel);
        }
    }
}
