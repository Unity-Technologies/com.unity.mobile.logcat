using System;
using System.Collections.Generic;
using System.IO;
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

        // Commands from a single multiline entry run one after another; the queue holds what's left.
        readonly Queue<string> m_PendingCommands = new Queue<string>();
        bool m_IsRunning;
        bool m_CancelRequested;

        bool m_EditMode;

        VisualElement m_FavoritesContainer;
        VisualElement m_GeneralContainer;
        VisualElement m_OutputContainer;
        ScrollView m_OutputScroll;
        Label m_RunningLabel;
        Button m_CancelButton;

        VisualElement m_SelectedFavRow;
        VisualElement m_SelectedGenRow;

        const int kMaxOutputLines = 3000;

        static readonly Color kCommandColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color kAccentColor = new Color(0.4f, 0.8f, 0.4f);
        static readonly Color kErrorColor = new Color(0.9f, 0.3f, 0.3f);
        static readonly Color kSelectedRowColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);

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

            // Don't leave the window latched in the running state if a command was still in flight.
            m_PendingCommands.Clear();
            m_CancelRequested = false;
            SetRunning(false);

            SaveCommands();
            m_Runtime.Closing -= OnDisable;
            m_DeviceSelection.Dispose();
            m_DeviceSelection = null;
            m_Runtime = null;
        }

        // --- UI Setup ---

        /// <summary>
        /// Shown instead of the normal UI when the Android module isn't available, so the window is
        /// never just blank.
        /// </summary>
        void LoadNotInstalledUI()
        {
            var r = rootVisualElement;
            r.Clear();
            r.Add(new HelpBox("Android Logcat requires Android support to be installed.", HelpBoxMessageType.Info));
        }

        void LoadUI()
        {
            var r = rootVisualElement;
            // Guard against OnEnable running twice on the same instance (e.g. re-docking), which
            // would otherwise clone the tree a second time and leave Q() returning stale elements.
            r.Clear();

            // The device selection popup is IMGUI-only, so the toolbar is hosted in an IMGUIContainer.
            // Its height must be pinned, otherwise it claims flexible space and squashes the panes.
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

            r.Q<Button>("ClearButton").clicked += ClearOutput;
            r.Q<Button>("CopyAllButton").clicked += CopyAllOutput;
            m_CancelButton.clicked += () => m_CancelRequested = true;

            // Right click anywhere in the output for copy actions.
            m_OutputContainer.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy All", _ => CopyAllOutput(),
                    m_OutputLines.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Clear", _ => ClearOutput(),
                    m_OutputLines.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));

            RebuildLists();
        }

        // --- Toolbar (IMGUI - contains DeviceSelection which is IMGUI-only) ---

        void DoToolbarGUI()
        {
            // Use the package's own styles so the buttons line up with the device selection popup,
            // which is drawn with AndroidLogcatStyles.
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            if (m_DeviceSelection != null)
                m_DeviceSelection.DoGUI();
            GUILayout.Space(3);

            if (GUILayout.Button("Add Command", AndroidLogcatStyles.toolbarButton))
                OnAddCommand();

            if (GUILayout.Button(m_EditMode ? "Edit Mode: ON" : "Edit Mode: OFF", AndroidLogcatStyles.toolbarButton))
            {
                m_EditMode = !m_EditMode;
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

        // --- Command Lists ---

        void RebuildLists()
        {
            RebuildCommandList(m_FavoritesContainer, m_Favorites, true, "No favorites. Use Edit Mode to move commands here.");
            RebuildCommandList(m_GeneralContainer, m_GeneralCommands, false, "No commands. Click 'Add Command' or 'Search Catalog'.");
        }

        void RebuildCommandList(VisualElement container, List<AndroidLogcatCommandEntry> list, bool isFavorites, string emptyMessage)
        {
            if (container == null)
                return;

            container.Clear();
            if (isFavorites)
                m_SelectedFavRow = null;
            else
                m_SelectedGenRow = null;

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

        void SetSelectedRow(VisualElement row, bool isFavorites)
        {
            var previous = isFavorites ? m_SelectedFavRow : m_SelectedGenRow;
            if (previous != null)
                previous.style.backgroundColor = StyleKeyword.Null;

            if (isFavorites)
                m_SelectedFavRow = row;
            else
                m_SelectedGenRow = row;

            row.style.backgroundColor = new StyleColor(kSelectedRowColor);
        }

        VisualElement CreateCommandRow(AndroidLogcatCommandEntry entry, List<AndroidLogcatCommandEntry> list, int index, bool isFavorites)
        {
            var row = CreateStyledRow(22);

            row.RegisterCallback<MouseDownEvent>(_ => SetSelectedRow(row, isFavorites));

            var nameLabel = new Label(entry.name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.width = 180;
            nameLabel.style.minWidth = 180;
            row.Add(nameLabel);

            // Collapse multiline commands into a single line preview.
            var cmdLines = AndroidLogcatCommandParser.SplitLines(entry.command);
            var cmdDisplay = cmdLines.Count == 0
                ? string.Empty
                : cmdLines.Count > 1 ? $"{cmdLines[0]} (+{cmdLines.Count - 1} more)" : cmdLines[0];

            var cmdLabel = new Label(cmdDisplay);
            cmdLabel.style.color = new StyleColor(kCommandColor);
            cmdLabel.style.flexGrow = 1;
            cmdLabel.style.overflow = Overflow.Hidden;
            cmdLabel.tooltip = entry.command;
            row.Add(cmdLabel);

            if (m_EditMode)
            {
                AddRowButton(row, "▲", 22, () =>
                {
                    list.RemoveAt(index);
                    list.Insert(index - 1, entry);
                    SaveAndRebuild();
                }, index > 0);

                AddRowButton(row, "▼", 22, () =>
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
                        if (m_Runtime == null)
                            return;
                        entry.name = updated.name;
                        entry.command = updated.command;
                        entry.category = updated.category;
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

            // Strips '\r' and splits on '\n' - see SplitOutputLines for why adb's "\r\r\n" matters.
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

            // Keep lines compact - the default Label margins would space the output out noticeably.
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;

            // Allow the user to select and copy output text.
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

        // --- Command Execution ---

        void RunCommand(AndroidLogcatCommandEntry entry)
        {
            if (m_IsRunning)
            {
                AppendOutput("[Error] A command is already running.", kErrorColor);
                return;
            }

            AndroidLogcatPlaceholderDialog.Show(entry.command, ExecuteCommand);
        }

        /// <summary>
        /// Queues each line of the command and starts draining the queue. Execution happens on the
        /// dispatcher's worker thread so the Editor stays responsive.
        /// </summary>
        void ExecuteCommand(string resolvedCommand)
        {
            if (m_Runtime == null)
                return;

            // Re-check here as well as in RunCommand: the placeholder dialog is modeless, so another
            // batch may have started while it was open.
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
                // Only report a cancellation if something was actually skipped.
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
            // The window may have been closed while the command was in flight.
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

        // --- Add / Import / Export ---

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

            // Drop null/incomplete entries so they can't throw later in the UI.
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

            // Mutate in place rather than reassigning: an open Search window holds references to
            // these lists, and would otherwise keep showing the pre-import contents.
            m_Favorites.Clear();
            m_Favorites.AddRange(favorites);
            m_GeneralCommands.Clear();
            m_GeneralCommands.AddRange(general);

            SaveAndRebuild();
            AndroidLogcatInternalLog.Log($"Commands imported from {path}");
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
