using System;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatStatusBar
    {
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
            rc.x += 10.0f;
            rc.width -= 10.0f;
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
