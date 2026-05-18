using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatAddCommandDialog : EditorWindow
    {
        string m_Name = "";
        string m_Command = "";
        Action<AndroidLogcatCommandEntry> m_OnSave;
        bool m_IsEdit;

        internal static void Show(Action<AndroidLogcatCommandEntry> onSave, AndroidLogcatCommandEntry existing = null)
        {
            var wnd = CreateInstance<AndroidLogcatAddCommandDialog>();
            wnd.titleContent = new GUIContent(existing != null ? "Edit Command" : "Add Command");
            wnd.m_OnSave = onSave;
            wnd.minSize = new Vector2(400, 200);
            wnd.maxSize = new Vector2(600, 400);

            if (existing != null)
            {
                wnd.m_Name = existing.name;
                wnd.m_Command = existing.command;
                wnd.m_IsEdit = true;
            }

            wnd.ShowUtility();
        }

        void OnEnable()
        {
            LoadUI();
        }

        void LoadUI()
        {
            var r = rootVisualElement;
            var tree = AndroidLogcatUtilities.LoadUXML("AndroidLogcatAddCommand.uxml");
            tree.CloneTree(r);

            var nameField = r.Q<TextField>("NameField");
            nameField.value = m_Name;
            nameField.RegisterValueChangedCallback(evt => m_Name = evt.newValue);

            var commandField = r.Q<TextField>("CommandField");
            commandField.value = m_Command;
            commandField.RegisterValueChangedCallback(evt => m_Command = evt.newValue);

            var saveButton = r.Q<Button>("SaveButton");
            saveButton.text = m_IsEdit ? "Save" : "Add";
            saveButton.clicked += OnSaveClicked;

            r.Q<Button>("CancelButton").clicked += () => Close();
        }

        void OnSaveClicked()
        {
            if (string.IsNullOrWhiteSpace(m_Name) || string.IsNullOrWhiteSpace(m_Command))
            {
                EditorUtility.DisplayDialog("Android Logcat", "Both name and command are required.", "OK");
                return;
            }

            m_OnSave?.Invoke(new AndroidLogcatCommandEntry(m_Name.Trim(), m_Command.Trim()));
            Close();
        }
    }
}
