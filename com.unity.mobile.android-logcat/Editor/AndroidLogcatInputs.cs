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
        class KeyResult
        {
            internal AndroidKeyCode Key { get; }
            internal char Character { get; }

            internal KeyResult()
            {
                Key = (AndroidKeyCode)0;
                Character = (char)0;
            }

            internal KeyResult(char character)
            {
                Key = (AndroidKeyCode)0;
                Character = character;
            }

            internal KeyResult(AndroidKeyCode keyCode)
            {
                Key = keyCode;
                Character = (char)0;
            }

            public static readonly KeyResult Empty = new KeyResult();
        }


        const float kMargin = 10;
        const float kButtonHeight = 20;
        const float kMinWindowHeight = 220.0f;
        const float kMaxWindowHeight = 300.0f;

        Splitter m_VerticalSplitter;
        Vector2 m_SendTextScrollView;

        internal AndroidLogcatInputs()
        {
            m_VerticalSplitter = new Splitter(Splitter.SplitterType.Vertical, kMinWindowHeight, kMaxWindowHeight);
        }

        private bool Key(GUIContent name)
        {
            return GUILayout.Button(name, EditorStyles.miniButtonMid, GUILayout.Height(kButtonHeight));
        }

        private bool Key(string name, string tooltip = null)
        {
            if (tooltip == null)
                tooltip = name;
            return Key(new GUIContent(name, tooltip));
        }

        private bool DoLetters(string letters, out char code)
        {
            code = (char)0;

            foreach (var c in letters.ToCharArray())
            {
                if (Key(c.ToString(), c.ToString()))
                {
                    code = c;
                    return true;
                }
            }

            return false;
        }

        private void Margin()
        {
            GUILayout.Space(kMargin);
        }

        KeyResult DoKeyboard(AndroidLogcatUserSettings.InputSettings settings)
        {
            var result = KeyResult.Empty;
            char charResult;

            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.Label("Keyboard Keys", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(kButtonHeight);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Esc", "Escape"))
                result = new KeyResult(AndroidKeyCode.ESCAPE);
            // F* keys
            for (int i = 1; i < 13; i++)
            {
                if (Key($"F{i}"))
                    result = new KeyResult(AndroidKeyCode.F1 + i - 1);
            }
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();

            if (settings.ShiftModifier)
            {
                if (DoLetters("~!@#$%^&*()_+", out charResult))
                    result = new KeyResult(charResult);
            }
            else
            {
                if (DoLetters("`1234567890-=", out charResult))
                    result = new KeyResult(charResult);
            }
            if (Key("Backspace"))
                result = new KeyResult(AndroidKeyCode.DEL);
            Margin();
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Tab"))
                result = new KeyResult(AndroidKeyCode.TAB);

            if (settings.ShiftModifier)
            {
                if (DoLetters("QWERTYUIOP{}", out charResult))
                    result = new KeyResult(charResult);
            }
            else
            {
                if (DoLetters("qwertyuiop[]", out charResult))
                    result = new KeyResult(charResult);
            }


            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Caps"))
                result = new KeyResult(AndroidKeyCode.CAPS_LOCK);
            if (settings.ShiftModifier)
            {
                if (DoLetters("ASDFGHJKL:\"|", out charResult))
                    result = new KeyResult(charResult);
            }
            else
            {
                if (DoLetters("asdfghjkl;'\\", out charResult))
                {
                    // adb shell send text ' is not handled correctly
                    if (charResult == '\'')
                        result = new KeyResult(AndroidKeyCode.APOSTROPHE);
                    else
                        result = new KeyResult(charResult);
                }
            }
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Shift"))
                result = new KeyResult(AndroidKeyCode.SHIFT_LEFT);
            if (settings.ShiftModifier)
            {
                if (DoLetters("ZXCVBNM<>?", out charResult))
                    result = new KeyResult(charResult);
            }
            else
            {
                if (DoLetters("zxcvbnm,./", out charResult))
                    result = new KeyResult(charResult);
            }

            if (Key("Shift"))
                result = new KeyResult(AndroidKeyCode.SHIFT_RIGHT);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Ctrl"))
                result = new KeyResult(AndroidKeyCode.CTRL_LEFT);
            if (Key("Alt"))
                result = new KeyResult(AndroidKeyCode.ALT_LEFT);
            if (GUILayout.Button("Space", EditorStyles.miniButtonMid, GUILayout.Width(300)))
                result = new KeyResult(AndroidKeyCode.SPACE);
            if (Key("Alt"))
                result = new KeyResult(AndroidKeyCode.ALT_RIGHT);
            if (Key("Ctrl"))
                result = new KeyResult(AndroidKeyCode.CTRL_RIGHT);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.Label("Modifiers", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            settings.ShiftModifier = EditorGUILayout.ToggleLeft(" Shift", settings.ShiftModifier);
            GUILayout.EndHorizontal();

            return result;
        }

        private KeyResult DoCursorKeys(AndroidLogcatUserSettings.InputSettings settings)
        {
            var result = KeyResult.Empty;
            GUILayout.Label("Cursor Keys", EditorStyles.boldLabel);
            GUILayout.Space(kButtonHeight);

            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.BeginVertical();
            if (Key("PrintSc"))
                result = new KeyResult(AndroidKeyCode.SYSRQ);
            if (Key("Insert"))
                result = new KeyResult(AndroidKeyCode.INSERT);
            if (Key("Delete"))
                result = new KeyResult(AndroidKeyCode.FORWARD_DEL);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (Key("ScrollLock"))
                result = new KeyResult(AndroidKeyCode.SCROLL_LOCK);
            if (Key("Home"))
                result = new KeyResult(AndroidKeyCode.MOVE_HOME);
            if (Key("End"))
                result = new KeyResult(AndroidKeyCode.MOVE_END);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (Key("Break"))
                result = new KeyResult(AndroidKeyCode.BREAK);
            if (Key("PgUp"))
                result = new KeyResult(AndroidKeyCode.PAGE_UP);
            if (Key("PgDn"))
                result = new KeyResult(AndroidKeyCode.PAGE_DOWN);
            GUILayout.EndVertical();
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Space(kButtonHeight);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("▲"))
                result = new KeyResult(AndroidKeyCode.DPAD_UP);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("◄"))
                result = new KeyResult(AndroidKeyCode.DPAD_LEFT);
            if (Key("▼"))
                result = new KeyResult(AndroidKeyCode.DPAD_DOWN);
            if (Key("►"))
                result = new KeyResult(AndroidKeyCode.DPAD_RIGHT);
            Margin();
            GUILayout.EndHorizontal();

            return result;
        }

        private KeyResult DoNumpad(AndroidLogcatUserSettings.InputSettings settings)
        {
            var result = KeyResult.Empty;
            GUILayout.Label("Numpad", EditorStyles.boldLabel);
            GUILayout.Space(kButtonHeight);

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Lock"))
                result = new KeyResult(AndroidKeyCode.NUM_LOCK);
            if (Key("/"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_DIVIDE);
            if (Key("*"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_MULTIPLY);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("7"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_7);
            if (Key("8"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_8);
            if (Key("9"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_9);

            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("4"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_4);
            if (Key("5"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_5);
            if (Key("6"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_6);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("1"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_1);
            if (Key("2"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_2);
            if (Key("3"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_3);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("0"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_0);
            if (Key(","))
                result = new KeyResult(AndroidKeyCode.NUMPAD_COMMA);
            if (Key("."))
                result = new KeyResult(AndroidKeyCode.NUMPAD_DOT);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("="))
                result = new KeyResult(AndroidKeyCode.NUMPAD_EQUALS);
            if (Key("-"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_SUBTRACT);
            if (Key("+"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_ADD);
            if (Key("Enter"))
                result = new KeyResult(AndroidKeyCode.NUMPAD_ENTER);
            Margin();
            GUILayout.EndHorizontal();

            return result;
        }

        private KeyResult DoSystemKeys(AndroidLogcatUserSettings.InputSettings settings)
        {
            var result = KeyResult.Empty;
            GUILayout.Label("Volume Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Up"))
                result = new KeyResult(AndroidKeyCode.VOLUME_UP);
            if (Key("Down"))
                result = new KeyResult(AndroidKeyCode.VOLUME_DOWN);
            if (Key("Mute"))
                result = new KeyResult(AndroidKeyCode.VOLUME_MUTE);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Brigthness", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Up"))
                result = new KeyResult(AndroidKeyCode.BRIGHTNESS_UP);
            if (Key("Down"))
                result = new KeyResult(AndroidKeyCode.BRIGHTNESS_DOWN);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("System Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            Margin();
            GUILayout.BeginVertical();
            if (Key("Power"))
                result = new KeyResult(AndroidKeyCode.POWER);
            if (Key("Camera"))
                result = new KeyResult(AndroidKeyCode.CAMERA);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (Key("Wake Up"))
                result = new KeyResult(AndroidKeyCode.WAKEUP);

            if (Key("Call"))
                result = new KeyResult(AndroidKeyCode.CALL);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (Key("Sleep"))
                result = new KeyResult(AndroidKeyCode.SLEEP);
            if (Key("End Call"))
                result = new KeyResult(AndroidKeyCode.ENDCALL);
            GUILayout.EndVertical();
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Text Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Cut"))
                result = new KeyResult(AndroidKeyCode.CUT);
            if (Key("Copy"))
                result = new KeyResult(AndroidKeyCode.COPY);
            if (Key("Paste"))
                result = new KeyResult(AndroidKeyCode.PASTE);
            if (Key("Clear"))
                result = new KeyResult(AndroidKeyCode.CLEAR);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Navigation Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key(new GUIContent("◄", "Send Back key event")))
                result = new KeyResult(AndroidKeyCode.BACK);
            if (Key(new GUIContent("●", "Send Home key event")))
                result = new KeyResult(AndroidKeyCode.HOME);
            if (Key(new GUIContent("■", "Send Overview key event")))
                result = new KeyResult(AndroidKeyCode.APP_SWITCH);
            Margin();
            GUILayout.EndHorizontal();

            return result;
        }

        private KeyResult DoTVKeys(AndroidLogcatUserSettings.InputSettings settings)
        {
            var result = KeyResult.Empty;
            GUILayout.Label("TV Keys", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            Margin();

            GUILayout.BeginVertical();
            if (Key(new GUIContent("Channel Up")))
                result = new KeyResult(AndroidKeyCode.CHANNEL_UP);
            if (Key(new GUIContent("Channel Down")))
                result = new KeyResult(AndroidKeyCode.CHANNEL_DOWN);

            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (Key(new GUIContent("Zoom In")))
                result = new KeyResult(AndroidKeyCode.ZOOM_IN);
            if (Key(new GUIContent("Zoom Out")))
                result = new KeyResult(AndroidKeyCode.ZOOM_OUT);
            GUILayout.EndVertical();

            Margin();
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.BeginVertical();
            if (Key(new GUIContent("Live TV")))
                result = new KeyResult(AndroidKeyCode.TV);
            if (Key(new GUIContent("PnP", "Picture In Picture")))
                result = new KeyResult(AndroidKeyCode.WINDOW);
            if (Key(new GUIContent("Captions")))
                result = new KeyResult(AndroidKeyCode.CAPTIONS);
            if (Key(new GUIContent("TV Power")))
                result = new KeyResult(AndroidKeyCode.TV_POWER);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            if (Key(new GUIContent("Guide")))
                result = new KeyResult(AndroidKeyCode.GUIDE);
            if (Key(new GUIContent("Bookmark")))
                result = new KeyResult(AndroidKeyCode.BOOKMARK);
            if (Key(new GUIContent("Settings")))
                result = new KeyResult(AndroidKeyCode.SETTINGS);
            if (Key(new GUIContent("TV Input")))
                result = new KeyResult(AndroidKeyCode.TV_INPUT);
            GUILayout.EndVertical();
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Program Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key(new GUIContent("Red", "Program Red Key")))
                result = new KeyResult(AndroidKeyCode.PROG_RED);
            if (Key(new GUIContent("Green", "Program Green Key")))
                result = new KeyResult(AndroidKeyCode.PROG_GREEN);
            if (Key(new GUIContent("Yellow", "Program Yellow Key")))
                result = new KeyResult(AndroidKeyCode.PROG_YELLOW);
            if (Key(new GUIContent("Blue", "Program Blue Key")))
                result = new KeyResult(AndroidKeyCode.PROG_BLUE);
            Margin();
            GUILayout.EndHorizontal();
            return result;
        }

        private void SendKeyEventIfNeeded(AndroidLogcatDispatcher dispatcher, IAndroidLogcatDevice device, KeyResult keyResult)
        {
            if (device == null)
                return;

            if (keyResult.Key != 0)
                device.SendKeyAsync(dispatcher, keyResult.Key, false);
            else if (keyResult.Character != 0)
                device.SendTextAsync(dispatcher, keyResult.Character.ToString());
        }

        void DoSendText(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, GUILayoutOption[] options)
        {
            var settings = runtime.UserSettings.DeviceInputSettings;
            GUILayout.BeginVertical(options);
            // Note: It seems there's a hard limit in adb of 300 characters
            const int kLimit = 300;
            GUILayout.Label($"Send Text (Left {kLimit - settings.SendText.Length}):", EditorStyles.boldLabel);
            m_SendTextScrollView = GUILayout.BeginScrollView(m_SendTextScrollView, GUILayout.ExpandHeight(false));
            settings.SendText = GUILayout.TextArea(settings.SendText, kLimit, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            if (GUILayout.Button(new GUIContent("Send", "Send text to android device")))
            {
                device.SendTextAsync(runtime.Dispatcher, settings.SendText);
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
        }

        void DoSection(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, Func<AndroidLogcatUserSettings.InputSettings, KeyResult> doSection, GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(options);
            SendKeyEventIfNeeded(runtime.Dispatcher, device, doSection(runtime.UserSettings.DeviceInputSettings));
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
        }

        bool DoDbgProcessOperations(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, ProcessInformation process, float height)
        {
            var settings = runtime.UserSettings.DeviceInputSettings;
            GUILayout.BeginVertical(GUILayout.Height(height));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Package:", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            GUILayout.Label("Name:", EditorStyles.boldLabel);
            settings.TargetProcess.name = GUILayout.TextField(settings.TargetProcess.name);
            GUILayout.Label("Process Id:", EditorStyles.boldLabel);
            settings.TargetProcess.processId = EditorGUILayout.IntField(settings.TargetProcess.processId);

            GUILayout.BeginVertical();

            var options = new[] { GUILayout.Width(150) };

            if (GUILayout.Button(new GUIContent("Copy from the selected", "Copy information from selected package"), options))
            {
                if (process != null)
                {
                    settings.TargetProcess.name = process.name;
                    settings.TargetProcess.processId = process.processId;
                    settings.TargetProcess.exited = process.exited;
                    GUIUtility.keyboardControl = 0;
                }
            }
            GUILayout.FlexibleSpace();


            if (GUILayout.Button(new GUIContent("Start",
                "Start package using 'adb shell monkey -p <packlage> -c android.intent.category.LAUNCHER 1'"), options))
            {
                device.ActivityManager.StartOrResumePackage(settings.TargetProcess.name);
                return true;
            }
            if (GUILayout.Button(new GUIContent("Force Stop", "Stop package using 'adb shell am force-stop <package>'"), options))
            {
                device.ActivityManager.StopPackage(settings.TargetProcess.name);
                return true;
            }
            if (GUILayout.Button(new GUIContent("Crash", "Crash package using 'adb shell am crash <package>'"), options))
            {
                device.ActivityManager.CrashPackage(settings.TargetProcess.name);
                return true;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Kill with signal", "Kill application using 'adb shell run-as <package> kill -s <signal> <pid>'"), options))
            {
                device.KillProcess(settings.TargetProcess.name, settings.TargetProcess.processId, settings.PosixKillSignal);
                return true;
            }
            settings.PosixKillSignal = (PosixSignal)EditorGUILayout.EnumPopup(settings.PosixKillSignal);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
            return false;
        }

        private GUILayoutOption[] GetOptions(float width, float height)
        {
            return new[] { GUILayout.Width(width), GUILayout.Height(height) };
        }

        private GUILayoutOption[] GetOptions(float height)
        {
            return new[] { GUILayout.Height(height) };
        }

        internal bool DoGUI(AndroidLogcatRuntimeBase runtime, ExtraWindowState extraWindowState)
        {
            var dispatcher = runtime.Dispatcher;
            var device = runtime.DeviceQuery.SelectedDevice;
            var process = runtime.UserSettings.LastSelectedProcess;
            var splitterRectVertical = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));

            m_VerticalSplitter.DoGUI(splitterRectVertical, ref extraWindowState.Height);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            DoSection(runtime, device, DoKeyboard, GetOptions(500, extraWindowState.Height));
            GUILayout.Space(4);
            DoSection(runtime, device, DoCursorKeys, GetOptions(100, extraWindowState.Height));
            GUILayout.Space(4);
            DoSection(runtime, device, DoNumpad, GetOptions(100, extraWindowState.Height));
            GUILayout.Space(4);
            DoSection(runtime, device, DoSystemKeys, GetOptions(200, extraWindowState.Height));
            GUILayout.Space(4);
            DoSection(runtime, device, DoTVKeys, GetOptions(200, extraWindowState.Height));
            GUILayout.Space(4);
            DoSendText(runtime, device, GetOptions(extraWindowState.Height));
            GUILayout.Space(4);


            // Something for the future
            var refreshProcesses = false;
            if (Unsupported.IsDeveloperMode())
            {
                refreshProcesses = DoDbgProcessOperations(runtime, device, process, extraWindowState.Height);
                GUILayout.Space(4);
            }

            GUILayout.EndHorizontal();
            return refreshProcesses;
        }
    }
}
