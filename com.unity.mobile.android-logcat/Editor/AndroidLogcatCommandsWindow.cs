using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCommandsWindow : EditorWindow
    {
        static readonly AndroidLogcatCommandEntry[] s_DefaultCommands = new[]
        {
            new AndroidLogcatCommandEntry("List Devices", "adb devices"),
            new AndroidLogcatCommandEntry("Kill Server", "adb kill-server"),
            new AndroidLogcatCommandEntry("Reboot Device", "adb reboot")
        };

        internal struct OutputLine
        {
            internal string text;
            internal Color color;
        }

        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatDeviceSelection m_DeviceSelection;

        List<AndroidLogcatCommandEntry> m_Favorites = new List<AndroidLogcatCommandEntry>();
        List<AndroidLogcatCommandEntry> m_GeneralCommands = new List<AndroidLogcatCommandEntry>();
        readonly List<OutputLine> m_OutputLines = new List<OutputLine>();

        bool m_EditMode;
        bool m_Running;
        int m_SelectedFavoriteIndex = -1;
        int m_SelectedGeneralIndex = -1;

        Vector2 m_FavoritesScrollPos;
        Vector2 m_GeneralScrollPos;
        Vector2 m_OutputScrollPos;
        float m_OutputHeight = 150;

        const int kRowHeight = 22;
        const int kMaxOutputLines = 3000;

        static readonly Color kCommandColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color kAccentColor = new Color(0.4f, 0.8f, 0.4f);
        static readonly Color kErrorColor = new Color(0.9f, 0.3f, 0.3f);

        internal static void ShowWindow()
        {
            var wnd = GetWindow<AndroidLogcatCommandsWindow>("Commands");
            wnd.minSize = new Vector2(600, 400);
        }

        void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, OnDeviceSelected, nameof(AndroidLogcatCommandsWindow) + "_DeviceId");
            m_Runtime.Closing += OnDisable;
            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);

            LoadCommands();
        }

        void OnDisable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;
            if (m_Runtime == null)
                return;

            SaveCommands();

            m_Runtime.Closing -= OnDisable;
            m_DeviceSelection.Dispose();
            m_DeviceSelection = null;
            m_Runtime = null;
        }

        void OnDeviceSelected(IAndroidLogcatDevice device)
        {
            Repaint();
        }

        void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            DoToolbar();

            var outputAreaRect = new Rect(0, position.height - m_OutputHeight, position.width, m_OutputHeight);
            var listsHeight = position.height - EditorGUIUtility.singleLineHeight - 4 - m_OutputHeight - 8;

            DoCommandLists(listsHeight);
            DoOutputSplitter(outputAreaRect.y - 4);
            DoOutputArea();
        }

        // --- Toolbar ---

        void DoToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            m_DeviceSelection.DoGUI();
            GUILayout.Space(3);

            if (GUILayout.Button("Add Command", EditorStyles.toolbarButton))
                OnAddCommand();

            var editLabel = m_EditMode ? "Edit Mode: ON" : "Edit Mode: OFF";
            if (GUILayout.Button(editLabel, EditorStyles.toolbarButton))
                m_EditMode = !m_EditMode;

            if (GUILayout.Button("Import", EditorStyles.toolbarButton))
                OnImportCommands();

            if (GUILayout.Button("Export", EditorStyles.toolbarButton))
                OnExportCommands();

            if (GUILayout.Button("Search Catalog", EditorStyles.toolbarButton))
                OpenSearchWindow();

            GUILayout.FlexibleSpace();

            if (m_Running)
                GUILayout.Label("Running...", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        // --- Command Lists ---

        void DoCommandLists(float totalHeight)
        {
            var favHeight = Mathf.Min(m_Favorites.Count * kRowHeight + kRowHeight + 4, totalHeight * 0.4f);
            if (m_Favorites.Count == 0)
                favHeight = kRowHeight + 4;

            // Favorites
            EditorGUILayout.LabelField("Favorites", EditorStyles.boldLabel);
            m_FavoritesScrollPos = EditorGUILayout.BeginScrollView(m_FavoritesScrollPos, GUILayout.Height(favHeight));
            if (m_Favorites.Count == 0)
            {
                EditorGUILayout.LabelField("No favorites. Use Edit Mode to move commands here.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < m_Favorites.Count; i++)
                    DoCommandRow(m_Favorites, i, true, ref m_SelectedFavoriteIndex);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(2);

            // General Commands
            EditorGUILayout.LabelField("General Commands", EditorStyles.boldLabel);
            m_GeneralScrollPos = EditorGUILayout.BeginScrollView(m_GeneralScrollPos);
            if (m_GeneralCommands.Count == 0)
            {
                EditorGUILayout.LabelField("No commands. Click 'Add Command' or 'Search Catalog'.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < m_GeneralCommands.Count; i++)
                    DoCommandRow(m_GeneralCommands, i, false, ref m_SelectedGeneralIndex);
            }
            EditorGUILayout.EndScrollView();
        }

        void DoCommandRow(List<AndroidLogcatCommandEntry> list, int index, bool isFavorites, ref int selectedIndex)
        {
            var entry = list[index];
            var isSelected = selectedIndex == index;

            if (isSelected)
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(kRowHeight));

            if (isSelected)
                GUI.backgroundColor = Color.white;

            // Click to select
            if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                selectedIndex = index;
                Repaint();
            }

            // Name
            EditorGUILayout.LabelField(entry.name, EditorStyles.boldLabel, GUILayout.Width(180));

            // Command preview
            var cmdDisplay = entry.command;
            if (cmdDisplay.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                var cmdLines = cmdDisplay.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                cmdDisplay = cmdLines.Length > 1
                    ? $"{cmdLines[0].Trim()} (+{cmdLines.Length - 1} more)"
                    : cmdLines[0].Trim();
            }
            var cmdStyle = new GUIStyle(EditorStyles.label);
            cmdStyle.normal.textColor = kCommandColor;
            EditorGUILayout.LabelField(cmdDisplay, cmdStyle);

            // Edit mode buttons
            if (m_EditMode)
            {
                // Move up
                EditorGUI.BeginDisabledGroup(index == 0);
                if (GUILayout.Button("\u25B2", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    list.RemoveAt(index);
                    list.Insert(index - 1, entry);
                    if (selectedIndex == index) selectedIndex = index - 1;
                    SaveCommands();
                }
                EditorGUI.EndDisabledGroup();

                // Move down
                EditorGUI.BeginDisabledGroup(index == list.Count - 1);
                if (GUILayout.Button("\u25BC", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    list.RemoveAt(index);
                    list.Insert(index + 1, entry);
                    if (selectedIndex == index) selectedIndex = index + 1;
                    SaveCommands();
                }
                EditorGUI.EndDisabledGroup();

                // Fav / Unfav
                var favLabel = isFavorites ? "Unfav" : "Fav";
                if (GUILayout.Button(favLabel, EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    if (isFavorites)
                    {
                        m_Favorites.RemoveAt(index);
                        m_GeneralCommands.Add(entry);
                        selectedIndex = -1;
                    }
                    else
                    {
                        m_GeneralCommands.RemoveAt(index);
                        m_Favorites.Add(entry);
                        selectedIndex = -1;
                    }
                    SaveCommands();
                }

                // Edit
                if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(35)))
                {
                    AndroidLogcatAddCommandDialog.Show(updated =>
                    {
                        entry.name = updated.name;
                        entry.command = updated.command;
                        SaveCommands();
                        Repaint();
                    }, entry);
                }

                // Delete
                if (GUILayout.Button("Del", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    if (EditorUtility.DisplayDialog("Delete Command",
                        $"Delete \"{entry.name}\"?", "Delete", "Cancel"))
                    {
                        list.RemoveAt(index);
                        selectedIndex = -1;
                        SaveCommands();
                    }
                }
            }

            // Run button (always visible)
            if (GUILayout.Button("Run", EditorStyles.miniButton, GUILayout.Width(35)))
                RunCommand(entry);

            EditorGUILayout.EndHorizontal();
        }

        // --- Output Area ---

        void DoOutputSplitter(float y)
        {
            var splitterRect = new Rect(0, y, position.width, 4);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);

            if (GUIUtility.hotControl != 0 && Event.current.type == EventType.MouseDrag)
            {
                m_OutputHeight = Mathf.Clamp(position.height - Event.current.mousePosition.y, 50, position.height - 150);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
                GUIUtility.hotControl = 0;
        }

        void DoOutputArea()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel, GUILayout.Width(50));
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)))
                m_OutputLines.Clear();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            m_OutputScrollPos = EditorGUILayout.BeginScrollView(m_OutputScrollPos, GUILayout.Height(m_OutputHeight - 20));

            for (int i = 0; i < m_OutputLines.Count; i++)
            {
                var line = m_OutputLines[i];
                var style = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = false
                };
                style.normal.textColor = line.color;
                EditorGUILayout.LabelField(line.text, style);
            }

            EditorGUILayout.EndScrollView();
        }

        // --- Command Execution ---

        void RunCommand(AndroidLogcatCommandEntry entry)
        {
            AndroidLogcatPlaceholderDialog.Show(entry.command, resolved => ExecuteCommand(resolved));
        }

        void ExecuteCommand(string resolvedCommand)
        {
            var lines = resolvedCommand.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            m_Running = true;
            Repaint();

            var device = m_DeviceSelection != null ? m_DeviceSelection.SelectedDevice : null;
            var deviceId = device != null ? device.Id : null;

            foreach (var rawLine in lines)
            {
                var cmd = rawLine.Trim();
                if (string.IsNullOrEmpty(cmd))
                    continue;

                AppendOutput($"> {cmd}", kAccentColor);

                try
                {
                    string result;
                    if (cmd.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
                    {
                        var adbArgs = cmd.Substring(4);

                        // Inject device serial if we have a selected device and the command doesn't already specify -s
                        if (!string.IsNullOrEmpty(deviceId) && !adbArgs.TrimStart().StartsWith("-s "))
                            adbArgs = $"-s {deviceId} {adbArgs}";

                        result = m_Runtime.Tools.ADB.Run(new[] { adbArgs }, "");
                    }
                    else
                    {
                        var parts = cmd.Split(new[] { ' ' }, 2);
                        var fileName = parts[0];
                        var arguments = parts.Length > 1 ? parts[1] : "";
                        var shellResult = Shell.RunProcess(fileName, arguments);
                        result = shellResult.GetStandardOut();
                        var err = shellResult.GetStandardErr();
                        if (!string.IsNullOrEmpty(err))
                            result = string.IsNullOrEmpty(result) ? err : result + "\n" + err;
                    }

                    AppendOutput(result, GUI.skin.label.normal.textColor);
                }
                catch (Exception ex)
                {
                    AppendOutput($"[Error] {ex.Message}", kErrorColor);
                }
            }

            m_Running = false;
            Repaint();
        }

        void AppendOutput(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (var line in text.Split('\n'))
                m_OutputLines.Add(new OutputLine { text = line, color = color });

            while (m_OutputLines.Count > kMaxOutputLines)
                m_OutputLines.RemoveAt(0);

            m_OutputScrollPos.y = float.MaxValue;

            AndroidLogcatCommandOutputWindow.AppendIfOpen(text, color);
        }

        // --- Add / Import / Export ---

        void OnAddCommand()
        {
            AndroidLogcatAddCommandDialog.Show(entry =>
            {
                m_GeneralCommands.Add(entry);
                SaveCommands();
                Repaint();
            });
        }

        void OpenSearchWindow()
        {
            AndroidLogcatCommandSearchWindow.Open(
                m_Favorites, m_GeneralCommands,
                entry =>
                {
                    m_GeneralCommands.Add(entry);
                    SaveCommands();
                    Repaint();
                },
                RunCommand);
        }

        void OnExportCommands()
        {
            var data = new AndroidLogcatCommandExportData
            {
                favorites = m_Favorites.ToArray(),
                general = m_GeneralCommands.ToArray()
            };

            var json = JsonUtility.ToJson(data, true);
            var path = EditorUtility.SaveFilePanel("Export Commands", "", "logcat-commands", "json");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, json);
            Debug.Log($"[Android Logcat] Commands exported to {path}");
        }

        void OnImportCommands()
        {
            var path = EditorUtility.OpenFilePanel("Import Commands", "", "json");
            if (string.IsNullOrEmpty(path))
                return;

            AndroidLogcatCommandExportData data;
            try
            {
                var json = File.ReadAllText(path);
                data = JsonUtility.FromJson<AndroidLogcatCommandExportData>(json);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Failed",
                    $"Could not parse the selected file:\n{ex.Message}", "OK");
                return;
            }

            if ((data.favorites == null || data.favorites.Length == 0) &&
                (data.general == null || data.general.Length == 0))
            {
                EditorUtility.DisplayDialog("Import Failed",
                    "The file contains no commands.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Import Commands",
                "This will replace all your current commands. Continue?", "Import", "Cancel"))
                return;

            m_Favorites.Clear();
            if (data.favorites != null)
                m_Favorites.AddRange(data.favorites);

            m_GeneralCommands.Clear();
            if (data.general != null)
                m_GeneralCommands.AddRange(data.general);

            SaveCommands();
            Debug.Log($"[Android Logcat] Commands imported from {path}");
        }

        // --- Persistence ---

        void LoadCommands()
        {
            var settings = m_Runtime.UserSettings.CommandsSettings;
            m_Favorites.Clear();
            m_GeneralCommands.Clear();

            if (settings.Favorites != null)
                m_Favorites.AddRange(settings.Favorites);
            if (settings.GeneralCommands != null)
                m_GeneralCommands.AddRange(settings.GeneralCommands);

            if (m_GeneralCommands.Count == 0 && m_Favorites.Count == 0)
                m_GeneralCommands.AddRange(s_DefaultCommands);
        }

        void SaveCommands()
        {
            var settings = m_Runtime.UserSettings.CommandsSettings;
            settings.Favorites = new List<AndroidLogcatCommandEntry>(m_Favorites);
            settings.GeneralCommands = new List<AndroidLogcatCommandEntry>(m_GeneralCommands);
        }
    }
}
