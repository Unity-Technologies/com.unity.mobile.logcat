using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatAddCommandDialog : EditorWindow
    {
        [SerializeField] string m_Name = "";
        [SerializeField] string m_Command = "";
        [SerializeField] AndroidLogcatCommandCategory m_Category = AndroidLogcatCommandCategory.Uncategorized;
        [SerializeField] bool m_IsEdit;

        Action<AndroidLogcatCommandEntry> m_OnSave;

        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Close;
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
        }

        internal static void Show(Action<AndroidLogcatCommandEntry> onSave, AndroidLogcatCommandEntry existing = null)
        {
            var wnd = CreateInstance<AndroidLogcatAddCommandDialog>();
            wnd.titleContent = new GUIContent(existing != null ? "Edit Command" : "Add Command");
            wnd.m_OnSave = onSave;
            wnd.minSize = new Vector2(400, 200);
            wnd.maxSize = new Vector2(600, 400);

            if (existing != null)
            {
                wnd.m_Name = existing.name ?? "";
                wnd.m_Command = existing.command ?? "";
                wnd.m_Category = existing.category;
                wnd.m_IsEdit = true;
            }

            wnd.ShowUtility();
        }

        void CreateGUI()
        {
            var r = rootVisualElement;
            r.Clear();

            var tree = AndroidLogcatUtilities.LoadUXML("Command/AndroidLogcatAddCommand.uxml");
            tree.CloneTree(r);

            var nameField = r.Q<TextField>("NameField");
            nameField.value = m_Name;
            nameField.RegisterValueChangedCallback(evt => m_Name = evt.newValue);

            var commandField = r.Q<TextField>("CommandField");
            commandField.value = m_Command;
            commandField.RegisterValueChangedCallback(evt => m_Command = evt.newValue);

            var categoryField = new EnumField("Category", m_Category);
            categoryField.RegisterValueChangedCallback(evt => m_Category = (AndroidLogcatCommandCategory)evt.newValue);
            r.Q<VisualElement>("CategoryContainer").Add(categoryField);

            var saveButton = r.Q<Button>("SaveButton");
            saveButton.text = m_IsEdit ? "Save" : "Add";
            saveButton.clicked += OnSaveClicked;

            r.Q<Button>("CancelButton").clicked += Close;

            nameField.schedule.Execute(() => nameField.Focus());
        }

        void OnSaveClicked()
        {
            if (string.IsNullOrWhiteSpace(m_Name) || string.IsNullOrWhiteSpace(m_Command))
            {
                EditorUtility.DisplayDialog("Android Logcat", "Both name and command are required.", "OK");
                return;
            }

            m_OnSave?.Invoke(new AndroidLogcatCommandEntry(m_Name.Trim(), m_Command.Trim(), m_Category));
            Close();
        }
    }
}
