using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Class for sending input to android device or running application
    /// </summary>
    internal class AndroidLogcatInputs
    {
        const float kMargin = 10;
        const float kButtonHeight = 20;
        const float kMinWindowHeight = 155.0f;
        const float kMaxWindowHeight = 200.0f;

        Splitter m_VerticalSplitter;
        // TODO: from setting
        string m_SendText;

        internal AndroidLogcatInputs()
        {
            m_VerticalSplitter = new Splitter(Splitter.SplitterType.Vertical, kMinWindowHeight, kMaxWindowHeight);
            m_SendText = string.Empty;
        }

        private bool Key(string name)
        {
            return GUILayout.Button(name, EditorStyles.miniButtonMid, GUILayout.Height(kButtonHeight));
        }

        private bool DoLetters(string letters, out AndroidKeyCode code)
        {
            code = (AndroidKeyCode)0;

            foreach (var c in letters.ToCharArray())
            {
                if (Key(c.ToString()))
                {
                    if (Enum.TryParse<AndroidKeyCode>(c.ToString(), out code))
                        return true;
                }
            }

            return false;
        }

        private void Margin()
        {
            GUILayout.Space(kMargin);
        }

        internal AndroidKeyCode DoKeyboard()
        {
            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.Label("Keyboard Keys", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(kButtonHeight);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Esc"))
                return AndroidKeyCode.ESCAPE;
            // F* keys
            for (int i = 1; i < 13; i++)
            {
                if (Key($"F{i}"))
                    return AndroidKeyCode.F1 + i - 1;
            }
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("`"))
                return AndroidKeyCode.GRAVE;
            // Numbers
            for (int i = 1; i < 11; i++)
            {
                // Display 0 at the end
                if (Key($"{i % 10}"))
                {
                    if (i == 10)
                        return AndroidKeyCode._0;
                    return AndroidKeyCode._0 + i;
                }
            }
            if (Key("-"))
                return AndroidKeyCode.MINUS;
            if (Key("="))
                return AndroidKeyCode.EQUALS;
            if (Key("Backspace"))
                return AndroidKeyCode.DEL;
            Margin();
            GUILayout.EndHorizontal();

            AndroidKeyCode result;
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Tab"))
                return AndroidKeyCode.TAB;
            if (DoLetters("QWERTYUIOP", out result))
                return result;
            if (Key("["))
                return AndroidKeyCode.LEFT_BRACKET;
            if (Key("]"))
                return AndroidKeyCode.RIGHT_BRACKET;
            if (Key("Enter"))
                return AndroidKeyCode.ENTER;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Caps"))
                return AndroidKeyCode.CAPS_LOCK;
            if (DoLetters("ASDFGHJIKL", out result))
                return result;
            if (Key(";"))
                return AndroidKeyCode.SEMICOLON;
            if (Key("'"))
                return AndroidKeyCode.APOSTROPHE;
            if (Key("\\"))
                return AndroidKeyCode.BACKSLASH;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Shift"))
                return AndroidKeyCode.SHIFT_LEFT;
            if (DoLetters("ZXCVBNM", out result))
                return result;

            if (Key(","))
                return AndroidKeyCode.COMMA;
            if (Key("."))
                return AndroidKeyCode.PERIOD;
            if (Key("/"))
                return AndroidKeyCode.SLASH;

            if (Key("Shift"))
                return AndroidKeyCode.SHIFT_RIGHT;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Ctrl"))
                return AndroidKeyCode.CTRL_LEFT;
            if (Key("Alt"))
                return AndroidKeyCode.ALT_LEFT;
            if (GUILayout.Button("Space", EditorStyles.miniButtonMid, GUILayout.Width(300)))
                return AndroidKeyCode.SPACE;
            if (Key("Alt"))
                return AndroidKeyCode.ALT_RIGHT;
            if (Key("Ctrl"))
                return AndroidKeyCode.CTRL_RIGHT;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.EndHorizontal();

            return (AndroidKeyCode)0;
        }

        private AndroidKeyCode DoMiddleKeys()
        {
            GUILayout.Label("", EditorStyles.boldLabel);
            GUILayout.Space(kButtonHeight);

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("SysRq"))
                return AndroidKeyCode.SYSRQ;
            if (Key("ScrollLock"))
                return AndroidKeyCode.SCROLL_LOCK;
            if (Key("Break"))
                return AndroidKeyCode.BREAK;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Insert"))
                return AndroidKeyCode.INSERT;
            if (Key("Home"))
                return AndroidKeyCode.MOVE_HOME;
            if (Key("PageUp"))
                return AndroidKeyCode.PAGE_UP;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Delete"))
                return AndroidKeyCode.FORWARD_DEL;
            if (Key("End"))
                return AndroidKeyCode.MOVE_END;
            if (Key("PageDown"))
                return AndroidKeyCode.PAGE_DOWN;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Space(kButtonHeight);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("▲"))
                return AndroidKeyCode.DPAD_UP;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("◄"))
                return AndroidKeyCode.DPAD_LEFT;
            if (Key("▼"))
                return AndroidKeyCode.DPAD_DOWN;
            if (Key("►"))
                return AndroidKeyCode.DPAD_RIGHT;
            Margin();
            GUILayout.EndHorizontal();

            return (AndroidKeyCode)0;
        }

        private AndroidKeyCode DoNumpad()
        {
            GUILayout.Label("Numpad", EditorStyles.boldLabel);
            GUILayout.Space(kButtonHeight);

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Lock"))
                return AndroidKeyCode.NUM_LOCK;
            if (Key("/"))
                return AndroidKeyCode.NUMPAD_DIVIDE;
            if (Key("*"))
                return AndroidKeyCode.NUMPAD_MULTIPLY;
            if (Key("-"))
                return AndroidKeyCode.NUMPAD_SUBTRACT;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("7"))
                return AndroidKeyCode.NUMPAD_7;
            if (Key("8"))
                return AndroidKeyCode.NUMPAD_8;
            if (Key("9"))
                return AndroidKeyCode.NUMPAD_9;
            if (Key("+"))
                return AndroidKeyCode.NUMPAD_ADD;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("4"))
                return AndroidKeyCode.NUMPAD_4;
            if (Key("5"))
                return AndroidKeyCode.NUMPAD_5;
            if (Key("6"))
                return AndroidKeyCode.NUMPAD_6;
            if (Key("Enter"))
                return AndroidKeyCode.NUMPAD_ENTER;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("1"))
                return AndroidKeyCode.NUMPAD_1;
            if (Key("2"))
                return AndroidKeyCode.NUMPAD_2;
            if (Key("3"))
                return AndroidKeyCode.NUMPAD_3;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("0"))
                return AndroidKeyCode.NUMPAD_0;
            if (Key(","))
                return AndroidKeyCode.NUMPAD_COMMA;
            if (Key("."))
                return AndroidKeyCode.NUMPAD_DOT;
            if (Key("="))
                return AndroidKeyCode.NUMPAD_EQUALS;
            Margin();
            GUILayout.EndHorizontal();

            return (AndroidKeyCode)0;
        }

        private AndroidKeyCode DoSystemKeys()
        {
            GUILayout.Label("Volume", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Up"))
                return AndroidKeyCode.VOLUME_UP;
            if (Key("Down"))
                return AndroidKeyCode.VOLUME_DOWN;
            if (Key("Mute"))
                return AndroidKeyCode.VOLUME_MUTE;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Brigthness", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Up"))
                return AndroidKeyCode.BRIGHTNESS_UP;
            if (Key("Down"))
                return AndroidKeyCode.BRIGHTNESS_DOWN;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("System Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Power"))
                return AndroidKeyCode.POWER;
            if (Key("Camera"))
                return AndroidKeyCode.CAMERA;
            if (Key("Call"))
                return AndroidKeyCode.CALL;
            if (Key("End Call"))
                return AndroidKeyCode.ENDCALL;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Text Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Cut"))
                return AndroidKeyCode.CUT;
            if (Key("Copy"))
                return AndroidKeyCode.COPY;
            if (Key("Paste"))
                return AndroidKeyCode.PASTE;
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Navigation Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("◄"))
                return AndroidKeyCode.BACK;
            if (Key("●"))
                return AndroidKeyCode.HOME;
            if (Key("■"))
                return AndroidKeyCode.MENU;
            Margin();
            GUILayout.EndHorizontal();

            return (AndroidKeyCode)0;
        }

        private void SendKeyEventIfNeeded(AndroidLogcatDispatcher dispatcher, IAndroidLogcatDevice device, AndroidKeyCode keyCode)
        {
            if (keyCode == 0)
                return;

            device.SendKeyAsync(dispatcher, keyCode);
        }

        void DoSection(AndroidLogcatDispatcher dispatcher, IAndroidLogcatDevice device, Func<AndroidKeyCode> doSection, float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            SendKeyEventIfNeeded(dispatcher, device, doSection());
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
        }

        void DoSendText(AndroidLogcatDispatcher dispatcher, IAndroidLogcatDevice device, float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("Send Text:", EditorStyles.boldLabel);
            m_SendText = GUILayout.TextArea(m_SendText, GUILayout.ExpandHeight(true));
            if (GUILayout.Button("Send"))
            {
                var lines = m_SendText.Replace("\r\n", "\n").Split(new[] { '\n' });
                for (int i = 0; i < lines.Length; i++)
                {
                    var formattedLine = lines[i];
                    formattedLine = formattedLine.Replace("\"", "\\\"");
                    formattedLine = $"'{formattedLine}'";
                    device.SendTextAsync(dispatcher, formattedLine);
                    if (i + 1 < lines.Length)
                        device.SendKeyAsync(dispatcher, AndroidKeyCode.ENTER);
                }
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
        }

        internal void DoGUI(AndroidLogcatDispatcher dispatcher, IAndroidLogcatDevice device, ExtraWindowState extraWindowState)
        {
            var splitterRectVertical = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));

            m_VerticalSplitter.DoGUI(splitterRectVertical, ref extraWindowState.Height);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            DoSection(dispatcher, device, DoKeyboard, 500, extraWindowState.Height);
            GUILayout.Space(4);
            DoSection(dispatcher, device, DoMiddleKeys, 100, extraWindowState.Height);
            GUILayout.Space(4);
            DoSection(dispatcher, device, DoNumpad, 100, extraWindowState.Height);
            GUILayout.Space(4);
            DoSection(dispatcher, device, DoSystemKeys, 200, extraWindowState.Height);
            GUILayout.Space(4);
            DoSendText(dispatcher, device, 200, extraWindowState.Height);
            GUILayout.EndHorizontal();

        }
    }
}
