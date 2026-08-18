using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Prompts the user to fill in &lt;placeholder&gt; tokens before a command is run.
    /// The parsing and substitution logic lives in <see cref="AndroidLogcatCommandPlaceholders"/>.
    /// </summary>
    internal class AndroidLogcatPlaceholderDialog : EditorWindow
    {
        string m_OriginalCommand;
        Action<string> m_OnConfirm;
        List<AndroidLogcatCommandPlaceholders.Placeholder> m_Placeholders = new List<AndroidLogcatCommandPlaceholders.Placeholder>();

        // Neither the callback nor the placeholder list survive a domain reload, so rather than
        // leave a dialog whose Run button silently does nothing, close it.
        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Close;
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
        }

        /// <summary>
        /// Shows the dialog if <paramref name="command"/> contains placeholders, otherwise invokes
        /// <paramref name="onConfirm"/> immediately with the unchanged command.
        /// </summary>
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
            wnd.minSize = new Vector2(400, 150);
            wnd.maxSize = new Vector2(600, 400);

            // Note: the UI is built in CreateGUI, which runs after this point, so the assignments
            // above are guaranteed to be visible to it. Building the UI in OnEnable would run before
            // CreateInstance returns and render an empty dialog.
            wnd.ShowUtility();
        }

        /// <summary>
        /// Prefills tokens we can infer from the project.
        /// </summary>
        static string GetDefaultValue(string token)
        {
            if (token.Equals("package", StringComparison.OrdinalIgnoreCase))
                return PlayerSettings.applicationIdentifier;
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
                var tf = new TextField($"<{ph.Token}>");
                tf.value = ph.Value;
                tf.RegisterValueChangedCallback(evt => ph.Value = evt.newValue);
                container.Add(tf);

                if (firstField == null)
                    firstField = tf;
            }

            r.Q<Button>("CancelButton").clicked += Close;
            r.Q<Button>("RunButton").clicked += OnRunClicked;

            // Scheduled because focusing before the first layout pass is unreliable.
            if (firstField != null)
                firstField.schedule.Execute(() => firstField.Focus());
        }

        void OnRunClicked()
        {
            var resolved = AndroidLogcatCommandPlaceholders.Resolve(m_OriginalCommand, m_Placeholders);
            m_OnConfirm?.Invoke(resolved);
            Close();
        }
    }
}
