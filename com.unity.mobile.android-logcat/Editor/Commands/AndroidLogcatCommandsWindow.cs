using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCommandsWindow : EditorWindow
    {
        static readonly AndroidLogcatCommandEntry[] s_DefaultCommands = new[]
        {
            new AndroidLogcatCommandEntry("List Devices", "adb devices", AndroidLogcatCommandCategory.DeviceManagement),
            new AndroidLogcatCommandEntry("Kill Server", "adb kill-server", AndroidLogcatCommandCategory.DeviceManagement),
            new AndroidLogcatCommandEntry("Reboot Device", "adb reboot", AndroidLogcatCommandCategory.DeviceManagement)
        };

        AndroidLogcatRuntimeBase m_Runtime;
        AndroidLogcatDeviceSelection m_DeviceSelection;

        List<AndroidLogcatCommandEntry> m_Favorites = new List<AndroidLogcatCommandEntry>();
        List<AndroidLogcatCommandEntry> m_GeneralCommands = new List<AndroidLogcatCommandEntry>();
        readonly List<AndroidLogcatOutputLine> m_OutputLines = new List<AndroidLogcatOutputLine>();

        readonly Queue<string> m_PendingCommands = new Queue<string>();
        bool m_IsRunning;
        bool m_CancelRequested;

        bool m_EditMode;

        readonly HashSet<AndroidLogcatCommandEntry> m_Selected = new HashSet<AndroidLogcatCommandEntry>();

        VisualElement m_FavoritesContainer;
        VisualElement m_GeneralContainer;
        VisualElement m_OutputContainer;
        ScrollView m_OutputScroll;
        Label m_RunningLabel;
        Button m_CancelButton;

        VisualElement m_EditActionsBar;
        Label m_SelectionCountLabel;
        Button m_DeleteSelectedButton;
        Button m_DeselectAllButton;

        const int kMaxOutputLines = 3000;

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
            {
                LoadNotInstalledUI();
                return;
            }

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

            m_PendingCommands.Clear();
            m_CancelRequested = false;
            SetRunning(false);

            SaveCommands();
            m_Runtime.Closing -= OnDisable;
            m_DeviceSelection.Dispose();
            m_DeviceSelection = null;
            m_Runtime = null;
        }

        void LoadNotInstalledUI()
        {
            var r = rootVisualElement;
            r.Clear();
            r.Add(new HelpBox("Android Logcat requires Android support to be installed.", HelpBoxMessageType.Info));
        }

        void LoadUI()
        {
            var r = rootVisualElement;
            r.Clear();

            var toolbar = new IMGUIContainer(DoToolbarGUI);
            toolbar.style.height = AndroidLogcatStyles.kFixedHeight;
            toolbar.style.flexShrink = 0;
            toolbar.style.flexGrow = 0;
            r.Add(toolbar);

            var tree = AndroidLogcatUtilities.LoadUXML("Command/AndroidLogcatCommands.uxml");
            tree.CloneTree(r);

            m_FavoritesContainer = r.Q<VisualElement>("FavoritesContainer");
            m_GeneralContainer = r.Q<VisualElement>("GeneralContainer");
            m_OutputContainer = r.Q<VisualElement>("OutputContainer");
            m_OutputScroll = r.Q<ScrollView>("OutputScroll");
            m_RunningLabel = r.Q<Label>("RunningLabel");
            m_CancelButton = r.Q<Button>("CancelButton");

            m_EditActionsBar = r.Q<VisualElement>("EditActionsBar");
            m_SelectionCountLabel = r.Q<Label>("SelectionCountLabel");
            m_DeleteSelectedButton = r.Q<Button>("DeleteSelectedButton");
            m_DeselectAllButton = r.Q<Button>("DeselectAllButton");

            m_DeleteSelectedButton.clicked += OnDeleteSelected;
            m_DeselectAllButton.clicked += () =>
            {
                m_Selected.Clear();
                RebuildLists();
            };

            r.Q<Button>("ClearButton").clicked += ClearOutput;
            r.Q<Button>("CopyAllButton").clicked += CopyAllOutput;
            m_CancelButton.clicked += () => m_CancelRequested = true;

            m_OutputContainer.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy All", _ => CopyAllOutput(),
                    m_OutputLines.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Clear", _ => ClearOutput(),
                    m_OutputLines.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));

            RebuildLists();
        }

        void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            if (m_DeviceSelection != null)
                m_DeviceSelection.DoGUI();
            GUILayout.Space(3);

            if (GUILayout.Button("Add Command", AndroidLogcatStyles.toolbarButton))
                OnAddCommand();

            if (GUILayout.Button(m_EditMode ? "Edit Mode: ON" : "Edit Mode: OFF", AndroidLogcatStyles.toolbarButton))
            {
                m_EditMode = !m_EditMode;
                m_Selected.Clear();
                RebuildLists();
            }

            if (GUILayout.Button("Import", AndroidLogcatStyles.toolbarButton))
                OnImportCommands();

            if (GUILayout.Button("Export", AndroidLogcatStyles.toolbarButton))
                OnExportCommands();

            if (GUILayout.Button("Search Catalog", AndroidLogcatStyles.toolbarButton))
                OpenSearchWindow();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void RebuildLists()
        {
            m_Selected.RemoveWhere(e => !m_Favorites.Contains(e) && !m_GeneralCommands.Contains(e));

            RebuildCommandList(m_FavoritesContainer, m_Favorites, true, "No favorites. Use Edit Mode to move commands here.");
            RebuildCommandList(m_GeneralContainer, m_GeneralCommands, false, "No commands. Click 'Add Command' or 'Search Catalog'.");
            UpdateEditActionsBar();
        }

        void UpdateEditActionsBar()
        {
            if (m_EditActionsBar == null)
                return;

            m_EditActionsBar.style.display = m_EditMode ? DisplayStyle.Flex : DisplayStyle.None;

            var count = m_Selected.Count;
            m_SelectionCountLabel.text = count == 0
                ? "Select commands to delete several at once"
                : count == 1 ? "1 selected" : $"{count} selected";

            m_DeleteSelectedButton.SetEnabled(count > 0);
            m_DeselectAllButton.SetEnabled(count > 0);
        }

        void RebuildCommandList(VisualElement container, List<AndroidLogcatCommandEntry> list, bool isFavorites, string emptyMessage)
        {
            if (container == null)
                return;

            container.Clear();

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
            var row = AndroidLogcatCommandUI.CreateRow();

            if (m_EditMode)
            {
                var toggle = new Toggle { value = m_Selected.Contains(entry) };
                toggle.style.flexShrink = 0;
                toggle.style.marginRight = 2;
                toggle.style.marginTop = 0;
                toggle.style.marginBottom = 0;
                toggle.RegisterValueChangedCallback(evt => SetSelected(entry, evt.newValue, row));
                row.Add(toggle);

                row.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != (int)MouseButton.LeftMouse)
                        return;

                    var target = evt.target as VisualElement;
                    if (target == null)
                        return;

                    if (target == toggle || toggle.Contains(target) ||
                        target is Button || target.GetFirstAncestorOfType<Button>() != null)
                        return;

                    toggle.value = !toggle.value;
                });

                ApplySelectionStyle(row, m_Selected.Contains(entry));
            }

            row.Add(AndroidLogcatCommandUI.CreateNameLabel(entry.name));

            var cmdLines = AndroidLogcatCommandParser.SplitLines(entry.command);
            var cmdDisplay = cmdLines.Count == 0
                ? string.Empty
                : cmdLines.Count > 1 ? $"{cmdLines[0]} (+{cmdLines.Count - 1} more)" : cmdLines[0];

            var cmdLabel = AndroidLogcatCommandUI.CreateCommandLabel(cmdDisplay);
            AndroidLogcatCommandUI.SetPointerTooltip(cmdLabel, entry.command);
            row.Add(cmdLabel);

            if (m_EditMode)
            {
                AndroidLogcatCommandUI.AddRowButton(row, "▲", 22, () =>
                {
                    list.RemoveAt(index);
                    list.Insert(index - 1, entry);
                    SaveAndRebuild();
                }, index > 0, "Move up");

                AndroidLogcatCommandUI.AddRowButton(row, "▼", 22, () =>
                {
                    list.RemoveAt(index);
                    list.Insert(index + 1, entry);
                    SaveAndRebuild();
                }, index < list.Count - 1, "Move down");

                AndroidLogcatCommandUI.AddRowButton(row, isFavorites ? "Unfav" : "Fav", 40, () =>
                {
                    if (isFavorites) { m_Favorites.Remove(entry); m_GeneralCommands.Add(entry); }
                    else { m_GeneralCommands.Remove(entry); m_Favorites.Add(entry); }
                    SaveAndRebuild();
                });

                AndroidLogcatCommandUI.AddRowButton(row, "Edit", 35, () =>
                {
                    AndroidLogcatAddCommandDialog.Show(updated =>
                    {
                        if (m_Runtime == null)
                            return;
                        entry.name = updated.name;
                        entry.command = updated.command;
                        entry.category = updated.category;
                        SaveAndRebuild();
                    }, entry);
                });
            }
            else
            {
                AndroidLogcatCommandUI.AddRowButton(row, "Run", 35, () => RunCommand(entry));
            }

            return row;
        }

        void SetSelected(AndroidLogcatCommandEntry entry, bool selected, VisualElement row)
        {
            if (selected)
                m_Selected.Add(entry);
            else
                m_Selected.Remove(entry);

            ApplySelectionStyle(row, selected);
            UpdateEditActionsBar();
        }

        static void ApplySelectionStyle(VisualElement row, bool selected)
        {
            row.style.backgroundColor = selected
                ? new StyleColor(AndroidLogcatCommandUI.kSelectedRowColor)
                : StyleKeyword.Null;
        }

        void OnDeleteSelected()
        {
            if (m_Selected.Count == 0)
                return;

            var names = m_Selected.Where(e => e != null).Select(e => e.name).ToList();
            const int kMaxNamesShown = 10;
            var preview = string.Join("\n", names.Take(kMaxNamesShown).Select(n => $"  • {n}"));
            if (names.Count > kMaxNamesShown)
                preview += $"\n  … and {names.Count - kMaxNamesShown} more";

            var title = names.Count == 1 ? "Delete Command" : "Delete Commands";
            var message = names.Count == 1
                ? $"Delete \"{names[0]}\"?"
                : $"Delete these {names.Count} commands?\n\n{preview}";

            if (!EditorUtility.DisplayDialog(title, message, "Delete", "Cancel"))
                return;

            m_Favorites.RemoveAll(e => m_Selected.Contains(e));
            m_GeneralCommands.RemoveAll(e => m_Selected.Contains(e));
            m_Selected.Clear();
            SaveAndRebuild();
        }

        void SaveAndRebuild()
        {
            SaveCommands();
            RebuildLists();
        }

        void ClearOutput()
        {
            m_OutputLines.Clear();
            m_OutputContainer?.Clear();
        }

        void CopyAllOutput()
        {
            var sb = new StringBuilder();
            foreach (var line in m_OutputLines)
                sb.AppendLine(line.Text);
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }

        void AppendOutput(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (var line in AndroidLogcatCommandParser.SplitOutputLines(text))
            {
                m_OutputLines.Add(new AndroidLogcatOutputLine(line, color));
                if (m_OutputContainer != null)
                    m_OutputContainer.Add(CreateOutputLabel(line, color));
            }

            if (m_OutputLines.Count > kMaxOutputLines)
            {
                var excess = m_OutputLines.Count - kMaxOutputLines;
                m_OutputLines.RemoveRange(0, excess);
                if (m_OutputContainer != null)
                    for (int i = 0; i < excess && m_OutputContainer.childCount > 0; i++)
                        m_OutputContainer.RemoveAt(0);
            }

            ScrollOutputToBottom();
        }

        static Label CreateOutputLabel(string text, Color color)
        {
            var label = new Label(text);
            label.style.color = new StyleColor(color);
            label.style.whiteSpace = WhiteSpace.Normal;

            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;

            label.selection.isSelectable = true;
            label.selection.doubleClickSelectsWord = true;
            return label;
        }

        void ScrollOutputToBottom()
        {
            if (m_OutputScroll == null || m_OutputContainer == null || m_OutputContainer.childCount == 0)
                return;
            m_OutputScroll.schedule
                .Execute(() => m_OutputScroll.ScrollTo(m_OutputContainer[m_OutputContainer.childCount - 1]))
                .StartingIn(10);
        }

        void RunCommand(AndroidLogcatCommandEntry entry)
        {
            if (m_IsRunning)
            {
                AppendOutput("[Error] A command is already running.", kErrorColor);
                return;
            }

            AndroidLogcatPlaceholderDialog.Show(entry.command, ExecuteCommand);
        }

        void ExecuteCommand(string resolvedCommand)
        {
            if (m_Runtime == null)
                return;

            if (m_IsRunning)
            {
                AppendOutput("[Error] A command is already running.", kErrorColor);
                return;
            }

            m_PendingCommands.Clear();
            foreach (var line in AndroidLogcatCommandParser.SplitLines(resolvedCommand))
                m_PendingCommands.Enqueue(line);

            if (m_PendingCommands.Count == 0)
                return;

            m_CancelRequested = false;
            SetRunning(true);
            RunNextPendingCommand();
        }

        void RunNextPendingCommand()
        {
            if (m_CancelRequested)
            {
                if (m_PendingCommands.Count > 0)
                {
                    AppendOutput("[Cancelled] Remaining commands were skipped.", kErrorColor);
                    m_PendingCommands.Clear();
                }
                m_CancelRequested = false;
                SetRunning(false);
                return;
            }

            if (m_PendingCommands.Count == 0)
            {
                SetRunning(false);
                return;
            }

            var cmd = m_PendingCommands.Dequeue();
            AppendOutput($"> {cmd}", kAccentColor);

            if (AndroidLogcatCommandParser.IsAdbCommand(cmd))
            {
                var device = m_DeviceSelection?.SelectedDevice;
                if (device == null)
                {
                    AppendOutput("[Error] No device selected.", kErrorColor);
                    m_PendingCommands.Clear();
                    SetRunning(false);
                    return;
                }

                device.RunAdbCommandAsync(m_Runtime.Dispatcher, AndroidLogcatCommandParser.StripAdbPrefix(cmd), OnCommandCompleted);
            }
            else
            {
                AndroidLogcatHostCommand.RunAsync(m_Runtime.Dispatcher, cmd, OnCommandCompleted);
            }
        }

        void OnCommandCompleted(AndroidLogcatCommandResult result)
        {
            if (m_Runtime == null)
                return;

            if (result == null)
                AppendOutput("[Error] Command produced no result.", kErrorColor);
            else if (result.Failed)
                AppendOutput($"[Error] {result.Error}", kErrorColor);
            else
                AppendOutput(result.Output, GetDefaultOutputColor());

            RunNextPendingCommand();
        }

        static Color GetDefaultOutputColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.1f, 0.1f, 0.1f);
        }

        void SetRunning(bool running)
        {
            m_IsRunning = running;

            if (m_RunningLabel != null)
                m_RunningLabel.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_CancelButton != null)
                m_CancelButton.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnAddCommand()
        {
            AndroidLogcatAddCommandDialog.Show(entry =>
            {
                if (m_Runtime == null)
                    return;
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
                    if (m_Runtime == null)
                        return;
                    m_GeneralCommands.Add(entry);
                    SaveAndRebuild();
                },
                entry =>
                {
                    if (m_Runtime == null)
                        return;
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
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                File.WriteAllText(path, json);
                AndroidLogcatInternalLog.Log($"Commands exported to {path}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Could not write the file:\n{ex.Message}", "OK");
            }
        }

        void OnImportCommands()
        {
            var path = EditorUtility.OpenFilePanel("Import Commands", "", "json");
            if (string.IsNullOrEmpty(path))
                return;

            AndroidLogcatCommandExportData data;
            try
            {
                data = JsonUtility.FromJson<AndroidLogcatCommandExportData>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Failed", $"Could not parse the selected file:\n{ex.Message}", "OK");
                return;
            }

            if (data == null)
            {
                EditorUtility.DisplayDialog("Import Failed", "The file contains no commands.", "OK");
                return;
            }

            var favorites = AndroidLogcatCommandImport.Sanitize(data.favorites);
            var general = AndroidLogcatCommandImport.Sanitize(data.general);

            if (favorites.Count == 0 && general.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Failed",
                    "The file contains no valid commands. Each entry needs both a name and a command.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Import Commands", "This will replace all your current commands. Continue?", "Import", "Cancel"))
                return;

            m_Favorites.Clear();
            m_Favorites.AddRange(favorites);
            m_GeneralCommands.Clear();
            m_GeneralCommands.AddRange(general);
            m_Selected.Clear();

            SaveAndRebuild();
            AndroidLogcatInternalLog.Log($"Commands imported from {path}");
        }

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
                foreach (var entry in s_DefaultCommands)
                    m_GeneralCommands.Add(entry.Clone());
        }

        void SaveCommands()
        {
            var settings = m_Runtime.UserSettings.CommandsSettings;
            settings.Favorites = new List<AndroidLogcatCommandEntry>(m_Favorites);
            settings.GeneralCommands = new List<AndroidLogcatCommandEntry>(m_GeneralCommands);
        }
    }
}
