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
        }


        const float kMargin = 10;
        const float kButtonHeight = 20;
        const float kMinWindowHeight = 220.0f;
        const float kMaxWindowHeight = 300.0f;

        Splitter m_VerticalSplitter;

        internal AndroidLogcatInputs()
        {
            m_VerticalSplitter = new Splitter(Splitter.SplitterType.Vertical, kMinWindowHeight, kMaxWindowHeight);
        }

        private bool Key(GUIContent name)
        {
            return GUILayout.Button(name, EditorStyles.miniButtonMid, GUILayout.Height(kButtonHeight));
        }

        private bool Key(string name)
        {
            return Key(new GUIContent(name));
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

        private bool DoLetters(string letters, out char code)
        {
            code = (char)0;

            foreach (var c in letters.ToCharArray())
            {
                if (Key(c.ToString()))
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
            char result;

            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.Label("Keyboard Keys", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(kButtonHeight);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Esc"))
                return new KeyResult(AndroidKeyCode.ESCAPE);
            // F* keys
            for (int i = 1; i < 13; i++)
            {
                if (Key($"F{i}"))
                    return new KeyResult(AndroidKeyCode.F1 + i - 1);
            }
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();

            if (settings.ShiftModifier)
            {
                if (DoLetters("~!@#$%^&*()_+", out result))
                    return new KeyResult(result);
            }
            else
            {
                if (DoLetters("`1234567890-=", out result))
                    return new KeyResult(result);
            }
            if (Key("Backspace"))
                return new KeyResult(AndroidKeyCode.DEL);
            Margin();
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Tab"))
                return new KeyResult(AndroidKeyCode.TAB);

            if (settings.ShiftModifier)
            {
                if (DoLetters("QWERTYUIOP{}", out result))
                    return new KeyResult(result);
            }
            else
            {
                if (DoLetters("qwertyuiop[]", out result))
                    return new KeyResult(result);
            }


            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Caps"))
                return new KeyResult(AndroidKeyCode.CAPS_LOCK);
            if (settings.ShiftModifier)
            {
                if (DoLetters("ASDFGHJKL:\"|", out result))
                    return new KeyResult(result);
            }
            else
            {
                if (DoLetters("asdfghjkl;'\\", out result))
                {
                    // adb shell send text ' is not handled correctly
                    if (result == '\'')
                        return new KeyResult(AndroidKeyCode.APOSTROPHE);
                    return new KeyResult(result);
                }
            }
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Shift"))
                return new KeyResult(AndroidKeyCode.SHIFT_LEFT);
            if (settings.ShiftModifier)
            {
                if (DoLetters("ZXCVBNM<>?", out result))
                    return new KeyResult(result);
            }
            else
            {
                if (DoLetters("zxcvbnm,./", out result))
                    return new KeyResult(result);
            }

            if (Key("Shift"))
                return new KeyResult(AndroidKeyCode.SHIFT_RIGHT);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Ctrl"))
                return new KeyResult(AndroidKeyCode.CTRL_LEFT);
            if (Key("Alt"))
                return new KeyResult(AndroidKeyCode.ALT_LEFT);
            if (GUILayout.Button("Space", EditorStyles.miniButtonMid, GUILayout.Width(300)))
                return new KeyResult(AndroidKeyCode.SPACE);
            if (Key("Alt"))
                return new KeyResult(AndroidKeyCode.ALT_RIGHT);
            if (Key("Ctrl"))
                return new KeyResult(AndroidKeyCode.CTRL_RIGHT);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            GUILayout.Label("Modifier Keys", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            settings.ShiftModifier = GUILayout.Toggle(settings.ShiftModifier, " Shift");
            GUILayout.EndHorizontal();

            return new KeyResult();
        }

        private KeyResult DoCursorKeys(AndroidLogcatUserSettings.InputSettings settings)
        {
            GUILayout.Label("Cursor Keys", EditorStyles.boldLabel);
            GUILayout.Space(kButtonHeight);

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("SysRq"))
                return new KeyResult(AndroidKeyCode.SYSRQ);
            if (Key("ScrollLock"))
                return new KeyResult(AndroidKeyCode.SCROLL_LOCK);
            if (Key("Break"))
                return new KeyResult(AndroidKeyCode.BREAK);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Insert"))
                return new KeyResult(AndroidKeyCode.INSERT);
            if (Key("Home"))
                return new KeyResult(AndroidKeyCode.MOVE_HOME);
            if (Key("PageUp"))
                return new KeyResult(AndroidKeyCode.PAGE_UP);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Delete"))
                return new KeyResult(AndroidKeyCode.FORWARD_DEL);
            if (Key("End"))
                return new KeyResult(AndroidKeyCode.MOVE_END);
            if (Key("PageDown"))
                return new KeyResult(AndroidKeyCode.PAGE_DOWN);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Space(kButtonHeight);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("▲"))
                return new KeyResult(AndroidKeyCode.DPAD_UP);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("◄"))
                return new KeyResult(AndroidKeyCode.DPAD_LEFT);
            if (Key("▼"))
                return new KeyResult(AndroidKeyCode.DPAD_DOWN);
            if (Key("►"))
                return new KeyResult(AndroidKeyCode.DPAD_RIGHT);
            Margin();
            GUILayout.EndHorizontal();

            return new KeyResult();
        }

        private KeyResult DoNumpad(AndroidLogcatUserSettings.InputSettings settings)
        {
            GUILayout.Label("Numpad", EditorStyles.boldLabel);
            GUILayout.Space(kButtonHeight);

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Lock"))
                return new KeyResult(AndroidKeyCode.NUM_LOCK);
            if (Key("/"))
                return new KeyResult(AndroidKeyCode.NUMPAD_DIVIDE);
            if (Key("*"))
                return new KeyResult(AndroidKeyCode.NUMPAD_MULTIPLY);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("7"))
                return new KeyResult(AndroidKeyCode.NUMPAD_7);
            if (Key("8"))
                return new KeyResult(AndroidKeyCode.NUMPAD_8);
            if (Key("9"))
                return new KeyResult(AndroidKeyCode.NUMPAD_9);

            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("4"))
                return new KeyResult(AndroidKeyCode.NUMPAD_4);
            if (Key("5"))
                return new KeyResult(AndroidKeyCode.NUMPAD_5);
            if (Key("6"))
                return new KeyResult(AndroidKeyCode.NUMPAD_6);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("1"))
                return new KeyResult(AndroidKeyCode.NUMPAD_1);
            if (Key("2"))
                return new KeyResult(AndroidKeyCode.NUMPAD_2);
            if (Key("3"))
                return new KeyResult(AndroidKeyCode.NUMPAD_3);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("0"))
                return new KeyResult(AndroidKeyCode.NUMPAD_0);
            if (Key(","))
                return new KeyResult(AndroidKeyCode.NUMPAD_COMMA);
            if (Key("."))
                return new KeyResult(AndroidKeyCode.NUMPAD_DOT);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Margin();
            if (Key("="))
                return new KeyResult(AndroidKeyCode.NUMPAD_EQUALS);
            if (Key("-"))
                return new KeyResult(AndroidKeyCode.NUMPAD_SUBTRACT);
            if (Key("+"))
                return new KeyResult(AndroidKeyCode.NUMPAD_ADD);
            if (Key("Enter"))
                return new KeyResult(AndroidKeyCode.NUMPAD_ENTER);
            Margin();
            GUILayout.EndHorizontal();

            return new KeyResult();
        }

        private KeyResult DoSystemKeys(AndroidLogcatUserSettings.InputSettings settings)
        {
            GUILayout.Label("Volume Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Up"))
                return new KeyResult(AndroidKeyCode.VOLUME_UP);
            if (Key("Down"))
                return new KeyResult(AndroidKeyCode.VOLUME_DOWN);
            if (Key("Mute"))
                return new KeyResult(AndroidKeyCode.VOLUME_MUTE);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Brigthness", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Up"))
                return new KeyResult(AndroidKeyCode.BRIGHTNESS_UP);
            if (Key("Down"))
                return new KeyResult(AndroidKeyCode.BRIGHTNESS_DOWN);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("System Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Power"))
                return new KeyResult(AndroidKeyCode.POWER);
            if (Key("Wake Up"))
                return new KeyResult(AndroidKeyCode.WAKEUP);
            Margin();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Camera"))
                return new KeyResult(AndroidKeyCode.CAMERA);
            if (Key("Call"))
                return new KeyResult(AndroidKeyCode.CALL);
            if (Key("End Call"))
                return new KeyResult(AndroidKeyCode.ENDCALL);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Text Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key("Cut"))
                return new KeyResult(AndroidKeyCode.CUT);
            if (Key("Copy"))
                return new KeyResult(AndroidKeyCode.COPY);
            if (Key("Paste"))
                return new KeyResult(AndroidKeyCode.PASTE);
            Margin();
            GUILayout.EndHorizontal();

            GUILayout.Label("Navigation Keys", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            Margin();
            if (Key(new GUIContent("◄", "Send Back key event")))
                return new KeyResult(AndroidKeyCode.BACK);
            if (Key(new GUIContent("●", "Send Home key event")))
                return new KeyResult(AndroidKeyCode.HOME);
            if (Key(new GUIContent("■", "Send Overview key event")))
                return new KeyResult(AndroidKeyCode.MENU);
            Margin();
            GUILayout.EndHorizontal();

            return new KeyResult();
        }

        private void SendKeyEventIfNeeded(AndroidLogcatDispatcher dispatcher, IAndroidLogcatDevice device, KeyResult keyResult)
        {
            if (device == null)
                return;

            if (keyResult.Key != 0)
                device.SendKeyAsync(dispatcher, keyResult.Key);
            else if (keyResult.Character != 0)
                device.SendTextAsync(dispatcher, keyResult.Character.ToString());
        }

        void DoSendText(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, float width, float height)
        {
            var settings = runtime.UserSettings.DeviceInputSettings;
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("Send Text:", EditorStyles.boldLabel);
            settings.SendText = GUILayout.TextArea(settings.SendText, GUILayout.ExpandHeight(true));
            if (GUILayout.Button(new GUIContent("Send", "Send text to android device")))
            {
                device.SendTextAsync(runtime.Dispatcher, settings.SendText);
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
        }

        void DoSection(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, Func<AndroidLogcatUserSettings.InputSettings, KeyResult> doSection, float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            SendKeyEventIfNeeded(runtime.Dispatcher, device, doSection(runtime.UserSettings.DeviceInputSettings));
            GUILayout.EndVertical();
            GUI.Box(GUILayoutUtility.GetLastRect(), GUIContent.none, EditorStyles.helpBox);
        }

        bool DoPackageOperations(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, PackageInformation package, float height)
        {
            var settings = runtime.UserSettings.DeviceInputSettings;
            GUILayout.BeginVertical(GUILayout.Height(height));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Package:", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            GUILayout.Label("Name:", EditorStyles.boldLabel);
            settings.TargetPackage.name = GUILayout.TextField(settings.TargetPackage.name);
            GUILayout.Label("Process Id:", EditorStyles.boldLabel);
            settings.TargetPackage.processId = EditorGUILayout.IntField(settings.TargetPackage.processId);

            GUILayout.BeginVertical();

            var options = new[] { GUILayout.Width(150) };

            if (GUILayout.Button(new GUIContent("Copy from the selected", "Copy information from selected package"), options))
            {
                if (package != null)
                {
                    settings.TargetPackage.name = package.name;
                    settings.TargetPackage.processId = package.processId;
                    settings.TargetPackage.exited = package.exited;
                    GUIUtility.keyboardControl = 0;
                }
            }
            GUILayout.FlexibleSpace();


            if (GUILayout.Button(new GUIContent("Start",
                "Start package using 'adb shell monkey -p <packlage> -c android.intent.category.LAUNCHER 1'"), options))
            {
                device.StartPackage(settings.TargetPackage.name);
                return true;
            }
            if (GUILayout.Button(new GUIContent("Force Stop", "Stop package using 'adb shell am force-stop <package>'"), options))
            {
                device.StopPackage(settings.TargetPackage.name);
                return true;
            }
            if (GUILayout.Button(new GUIContent("Crash", "Crash package using 'adb shell am crash <package>'"), options))
            {
                device.CrashPackage(settings.TargetPackage.name);
                return true;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Kill with signal", "Kill application using 'adb shell run-as <package> kill -s <signal> <pid>'"), options))
            {
                device.KillProcess(settings.TargetPackage.name, settings.TargetPackage.processId, settings.PosixKillSignal);
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

        internal bool DoGUI(AndroidLogcatRuntimeBase runtime, ExtraWindowState extraWindowState)
        {
            var dispatcher = runtime.Dispatcher;
            var device = runtime.DeviceQuery.SelectedDevice;
            var package = runtime.UserSettings.LastSelectedPackage;
            var splitterRectVertical = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));

            m_VerticalSplitter.DoGUI(splitterRectVertical, ref extraWindowState.Height);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            DoSection(runtime, device, DoKeyboard, 500, extraWindowState.Height);
            GUILayout.Space(4);
            DoSection(runtime, device, DoCursorKeys, 100, extraWindowState.Height);
            GUILayout.Space(4);
            DoSection(runtime, device, DoNumpad, 100, extraWindowState.Height);
            GUILayout.Space(4);
            DoSection(runtime, device, DoSystemKeys, 200, extraWindowState.Height);
            GUILayout.Space(4);
            DoSendText(runtime, device, 200, extraWindowState.Height);
            GUILayout.Space(4);
            var refreshPackages = DoPackageOperations(runtime, device, package, extraWindowState.Height);
            GUILayout.Space(4);
            GUILayout.EndHorizontal();
            return refreshPackages;
        }
    }
}
