using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPlaceholderDialog : EditorWindow
    {
        string m_OriginalCommand;
        Action<string> m_OnConfirm;
        List<AndroidLogcatCommandPlaceholders.Placeholder> m_Placeholders = new List<AndroidLogcatCommandPlaceholders.Placeholder>();

        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Close;
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
        }

        internal static void Show(string command, Action<string> onConfirm)
        {
            if (!AndroidLogcatCommandPlaceholders.HasPlaceholders(command))
            {
                onConfirm?.Invoke(command);
                return;
            }

            var wnd = CreateInstance<AndroidLogcatPlaceholderDialog>();
            wnd.titleContent = new GUIContent("Resolve Placeholders");
            wnd.m_OriginalCommand = command;
            wnd.m_OnConfirm = onConfirm;
            wnd.m_Placeholders = AndroidLogcatCommandPlaceholders.Parse(command, GetDefaultValue);
            wnd.minSize = new Vector2(440, 200);
            wnd.maxSize = new Vector2(700, 520);

            wnd.ShowUtility();
        }

        static string GetDefaultValue(string token)
        {
            if (token.Equals("package", StringComparison.OrdinalIgnoreCase))
                return PlayerSettings.applicationIdentifier;
            if (token.Equals("package/activity", StringComparison.OrdinalIgnoreCase))
                return $"{PlayerSettings.applicationIdentifier}/com.unity3d.player.UnityPlayerActivity";
            return string.Empty;
        }

        void CreateGUI()
        {
            var r = rootVisualElement;
            r.Clear();

            var tree = AndroidLogcatUtilities.LoadUXML("Command/AndroidLogcatPlaceholder.uxml");
            tree.CloneTree(r);

            r.Q<Label>("CommandPreview").text = m_OriginalCommand ?? "";

            var container = r.Q<VisualElement>("PlaceholderContainer");
            TextField firstField = null;

            foreach (var placeholder in m_Placeholders)
            {
                var ph = placeholder;
                var description = AndroidLogcatCommandPlaceholderHints.GetDescription(ph.Token);
                var example = AndroidLogcatCommandPlaceholderHints.GetExample(ph.Token);
                var suggestions = AndroidLogcatCommandPlaceholderHints.GetSuggestions(ph.Token);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                var tf = new TextField($"<{ph.Token}>");
                tf.value = ph.Value;
                tf.style.flexGrow = 1;
                tf.style.minWidth = 0;
                tf.RegisterValueChangedCallback(evt => ph.Value = evt.newValue);
                if (!string.IsNullOrEmpty(description))
                    tf.tooltip = description;
                row.Add(tf);

                if (suggestions.Length > 0)
                    row.Add(CreateSuggestionsButton(tf, suggestions));

                container.Add(row);

                if (!string.IsNullOrEmpty(example))
                    container.Add(CreateExampleLabel(example));

                if (firstField == null)
                    firstField = tf;
            }

            r.Q<Button>("CancelButton").clicked += Close;
            r.Q<Button>("RunButton").clicked += OnRunClicked;

            if (firstField != null)
                firstField.schedule.Execute(() => firstField.Focus());
        }

        static Button CreateSuggestionsButton(TextField field, string[] suggestions)
        {
            Button button = null;
            button = new Button(() =>
            {
                var menu = new GenericDropdownMenu();
                foreach (var suggestion in suggestions)
                {
                    var value = suggestion;
                    menu.AddItem(value, string.Equals(field.value, value, StringComparison.Ordinal),
                        () => field.value = value);
                }

                menu.DropDown(field.worldBound, button, true);
            })
            { text = "▾" };

            button.style.width = 22;
            button.style.minWidth = 22;
            button.style.flexShrink = 0;
            button.tooltip = "Common values";
            return button;
        }

        static Label CreateExampleLabel(string example)
        {
            var label = new Label($"e.g. {example}");
            label.style.fontSize = 10;
            label.style.color = new StyleColor(AndroidLogcatCommandUI.kHintColor);
            label.style.marginLeft = 4;
            label.style.marginBottom = 4;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        void OnRunClicked()
        {
            var resolved = AndroidLogcatCommandPlaceholders.Resolve(m_OriginalCommand, m_Placeholders);
            m_OnConfirm?.Invoke(resolved);
            Close();
        }
    }
}
