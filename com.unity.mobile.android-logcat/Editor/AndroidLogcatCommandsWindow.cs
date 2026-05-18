using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

        VisualElement m_FavoritesContainer;
        VisualElement m_GeneralContainer;
        VisualElement m_OutputContainer;
        ScrollView m_OutputScroll;
        Label m_RunningLabel;

        VisualElement m_SelectedFavRow;
        VisualElement m_SelectedGenRow;

        const int kMaxOutputLines = 3000;

        static readonly Color kCommandColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color kAccentColor = new Color(0.4f, 0.8f, 0.4f);
        static readonly Color kErrorColor = new Color(0.9f, 0.3f, 0.3f);
        static readonly Color kSelectedRowColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);
        static readonly char[] kNewlineChars = { '\r', '\n' };

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
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, _ => Repaint(), nameof(AndroidLogcatCommandsWindow) + "_DeviceId");
            m_Runtime.Closing += OnDisable;
            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);

            LoadCommands();
            LoadUI();
        }

        void OnDisable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled || m_Runtime == null)
                return;

            SaveCommands();
            m_Runtime.Closing -= OnDisable;
            m_DeviceSelection.Dispose();
            m_DeviceSelection = null;
            m_Runtime = null;
        }

        // --- UI Setup ---

        void LoadUI()
        {
            var r = rootVisualElement;
            r.Insert(0, new IMGUIContainer(DoToolbarGUI));

            var tree = AndroidLogcatUtilities.LoadUXML("AndroidLogcatCommands.uxml");
            tree.CloneTree(r);

            m_FavoritesContainer = r.Q<VisualElement>("FavoritesContainer");
            m_GeneralContainer = r.Q<VisualElement>("GeneralContainer");
            m_OutputContainer = r.Q<VisualElement>("OutputContainer");
            m_OutputScroll = r.Q<ScrollView>("OutputScroll");
            m_RunningLabel = r.Q<Label>("RunningLabel");

            r.Q<Button>("ClearButton").clicked += () => { m_OutputLines.Clear(); m_OutputContainer.Clear(); };
            r.Q<Button>("PopOutButton").clicked += () => AndroidLogcatCommandOutputWindow.Open(m_OutputLines);

            RebuildCommandList(m_FavoritesContainer, m_Favorites, true, "No favorites. Use Edit Mode to move commands here.", ref m_SelectedFavRow);
            RebuildCommandList(m_GeneralContainer, m_GeneralCommands, false, "No commands. Click 'Add Command' or 'Search Catalog'.", ref m_SelectedGenRow);
        }

        // --- Toolbar (IMGUI - contains DeviceSelection which is IMGUI-only) ---

        void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (m_DeviceSelection != null)
                m_DeviceSelection.DoGUI();
            GUILayout.Space(3);

            if (GUILayout.Button("Add Command", EditorStyles.toolbarButton))
                OnAddCommand();

            if (GUILayout.Button(m_EditMode ? "Edit Mode: ON" : "Edit Mode: OFF", EditorStyles.toolbarButton))
            {
                m_EditMode = !m_EditMode;
                RebuildLists();
            }

            if (GUILayout.Button("Import", EditorStyles.toolbarButton))
                OnImportCommands();

            if (GUILayout.Button("Export", EditorStyles.toolbarButton))
                OnExportCommands();

            if (GUILayout.Button("Search Catalog", EditorStyles.toolbarButton))
                OpenSearchWindow();

            if (GUILayout.Button("Open Output", EditorStyles.toolbarButton))
                AndroidLogcatCommandOutputWindow.Open(m_OutputLines);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // --- Command Lists ---

        void RebuildLists()
        {
            RebuildCommandList(m_FavoritesContainer, m_Favorites, true, "No favorites. Use Edit Mode to move commands here.", ref m_SelectedFavRow);
            RebuildCommandList(m_GeneralContainer, m_GeneralCommands, false, "No commands. Click 'Add Command' or 'Search Catalog'.", ref m_SelectedGenRow);
        }

        void RebuildCommandList(VisualElement container, List<AndroidLogcatCommandEntry> list, bool isFavorites, string emptyMessage, ref VisualElement selectedRow)
        {
            if (container == null)
                return;

            container.Clear();
            selectedRow = null;

            if (list.Count == 0)
            {
                var label = new Label(emptyMessage);
                label.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                label.style.fontSize = 10;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.paddingTop = 4;
                label.style.paddingBottom = 4;
                container.Add(label);
                return;
            }

            for (int i = 0; i < list.Count; i++)
                container.Add(CreateCommandRow(list[i], list, i, isFavorites));
        }

        VisualElement CreateCommandRow(AndroidLogcatCommandEntry entry, List<AndroidLogcatCommandEntry> list, int index, bool isFavorites)
        {
            var row = CreateStyledRow(22);

            // Click to select
            row.RegisterCallback<MouseDownEvent>(_ =>
            {
                ref var selected = ref (isFavorites ? ref m_SelectedFavRow : ref m_SelectedGenRow);
                if (selected != null)
                    selected.style.backgroundColor = StyleKeyword.Null;
                selected = row;
                row.style.backgroundColor = new StyleColor(kSelectedRowColor);
            });

            // Name
            var nameLabel = new Label(entry.name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.width = 180;
            nameLabel.style.minWidth = 180;
            row.Add(nameLabel);

            // Command preview
            var cmdDisplay = entry.command;
            if (cmdDisplay.IndexOfAny(kNewlineChars) >= 0)
            {
                var cmdLines = cmdDisplay.Split(kNewlineChars, StringSplitOptions.RemoveEmptyEntries);
                if (cmdLines.Length > 1)
                    cmdDisplay = $"{cmdLines[0].Trim()} (+{cmdLines.Length - 1} more)";
                else
                    cmdDisplay = cmdLines[0].Trim();
            }
            var cmdLabel = new Label(cmdDisplay);
            cmdLabel.style.color = new StyleColor(kCommandColor);
            cmdLabel.style.flexGrow = 1;
            cmdLabel.style.overflow = Overflow.Hidden;
            row.Add(cmdLabel);

            // Edit mode buttons
            if (m_EditMode)
            {
                AddRowButton(row, "\u25B2", 22, () =>
                {
                    list.RemoveAt(index);
                    list.Insert(index - 1, entry);
                    SaveAndRebuild();
                }, index > 0);

                AddRowButton(row, "\u25BC", 22, () =>
                {
                    list.RemoveAt(index);
                    list.Insert(index + 1, entry);
                    SaveAndRebuild();
                }, index < list.Count - 1);

                AddRowButton(row, isFavorites ? "Unfav" : "Fav", 40, () =>
                {
                    if (isFavorites) { m_Favorites.Remove(entry); m_GeneralCommands.Add(entry); }
                    else { m_GeneralCommands.Remove(entry); m_Favorites.Add(entry); }
                    SaveAndRebuild();
                });

                AddRowButton(row, "Edit", 35, () =>
                {
                    AndroidLogcatAddCommandDialog.Show(updated =>
                    {
                        if (m_Runtime == null) return;
                        entry.name = updated.name;
                        entry.command = updated.command;
                        SaveAndRebuild();
                    }, entry);
                });

                AddRowButton(row, "Del", 30, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Command", $"Delete \"{entry.name}\"?", "Delete", "Cancel"))
                    {
                        list.Remove(entry);
                        SaveAndRebuild();
                    }
                });
            }

            AddRowButton(row, "Run", 35, () => RunCommand(entry));
            return row;
        }

        void SaveAndRebuild()
        {
            SaveCommands();
            RebuildLists();
        }

        // --- Row Helpers ---

        static VisualElement CreateStyledRow(int height = 0)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            if (height > 0)
                row.style.height = height;
            return row;
        }

        static void AddRowButton(VisualElement row, string text, int width, Action action, bool enabled = true)
        {
            var btn = new Button(action) { text = text };
            btn.style.width = width;
            btn.SetEnabled(enabled);
            row.Add(btn);
        }

        // --- Output ---

        void AppendOutput(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (var line in text.Split('\n'))
            {
                m_OutputLines.Add(new OutputLine { text = line, color = color });
                if (m_OutputContainer != null)
                {
                    var label = new Label(line);
                    label.style.color = new StyleColor(color);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    m_OutputContainer.Add(label);
                }
            }

            if (m_OutputLines.Count > kMaxOutputLines)
            {
                var excess = m_OutputLines.Count - kMaxOutputLines;
                m_OutputLines.RemoveRange(0, excess);
                if (m_OutputContainer != null)
                    for (int i = 0; i < excess && m_OutputContainer.childCount > 0; i++)
                        m_OutputContainer.RemoveAt(0);
            }

            if (m_OutputScroll != null && m_OutputContainer != null && m_OutputContainer.childCount > 0)
                m_OutputScroll.schedule.Execute(() => m_OutputScroll.ScrollTo(m_OutputContainer[m_OutputContainer.childCount - 1])).StartingIn(10);

            AndroidLogcatCommandOutputWindow.AppendIfOpen(text, color);
        }

        // --- Command Execution ---

        void RunCommand(AndroidLogcatCommandEntry entry)
        {
            AndroidLogcatPlaceholderDialog.Show(entry.command, resolved => ExecuteCommand(resolved));
        }

        // TODO: Make async using AndroidLogcatDispatcher to avoid blocking the main thread
        void ExecuteCommand(string resolvedCommand)
        {
            if (m_Runtime == null) return;

            var lines = resolvedCommand.Split(kNewlineChars, StringSplitOptions.RemoveEmptyEntries);
            if (m_RunningLabel != null)
                m_RunningLabel.style.display = DisplayStyle.Flex;

            var deviceId = m_DeviceSelection?.SelectedDevice?.Id;

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
                        if (!string.IsNullOrEmpty(deviceId) && !adbArgs.TrimStart().StartsWith("-s "))
                            adbArgs = $"-s {deviceId} {adbArgs}";
                        result = m_Runtime.Tools.ADB.Run(new[] { adbArgs }, "");
                    }
                    else
                    {
                        var parts = cmd.Split(new[] { ' ' }, 2);
                        var shellResult = Shell.RunProcess(parts[0], parts.Length > 1 ? parts[1] : "");
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

            if (m_RunningLabel != null)
                m_RunningLabel.style.display = DisplayStyle.None;
        }

        // --- Add / Import / Export ---

        void OnAddCommand()
        {
            AndroidLogcatAddCommandDialog.Show(entry =>
            {
                if (m_Runtime == null) return;
                m_GeneralCommands.Add(entry);
                SaveAndRebuild();
            });
        }

        void OpenSearchWindow()
        {
            AndroidLogcatCommandSearchWindow.Open(
                m_Favorites, m_GeneralCommands,
                entry =>
                {
                    if (m_Runtime == null) return;
                    m_GeneralCommands.Add(entry);
                    SaveAndRebuild();
                },
                entry =>
                {
                    if (m_Runtime == null) return;
                    RunCommand(entry);
                });
        }

        void OnExportCommands()
        {
            var json = JsonUtility.ToJson(new AndroidLogcatCommandExportData
            {
                favorites = m_Favorites.ToArray(),
                general = m_GeneralCommands.ToArray()
            }, true);

            var path = EditorUtility.SaveFilePanel("Export Commands", "", "logcat-commands", "json");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                Debug.Log($"[Android Logcat] Commands exported to {path}");
            }
        }

        void OnImportCommands()
        {
            var path = EditorUtility.OpenFilePanel("Import Commands", "", "json");
            if (string.IsNullOrEmpty(path))
                return;

            AndroidLogcatCommandExportData data;
            try { data = JsonUtility.FromJson<AndroidLogcatCommandExportData>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Failed", $"Could not parse the selected file:\n{ex.Message}", "OK");
                return;
            }

            if ((data.favorites == null || data.favorites.Length == 0) && (data.general == null || data.general.Length == 0))
            {
                EditorUtility.DisplayDialog("Import Failed", "The file contains no commands.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Import Commands", "This will replace all your current commands. Continue?", "Import", "Cancel"))
                return;

            m_Favorites.Clear();
            if (data.favorites != null) m_Favorites.AddRange(data.favorites);
            m_GeneralCommands.Clear();
            if (data.general != null) m_GeneralCommands.AddRange(data.general);

            SaveAndRebuild();
            Debug.Log($"[Android Logcat] Commands imported from {path}");
        }

        // --- Persistence ---

        void LoadCommands()
        {
            var settings = m_Runtime.UserSettings.CommandsSettings;
            m_Favorites.Clear();
            m_GeneralCommands.Clear();

            if (settings.Favorites != null) m_Favorites.AddRange(settings.Favorites);
            if (settings.GeneralCommands != null) m_GeneralCommands.AddRange(settings.GeneralCommands);

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
