using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCommandSearchWindow : EditorWindow
    {
        static AndroidLogcatCommandSearchWindow s_Instance;

        List<AndroidLogcatCommandEntry> m_Favorites;
        List<AndroidLogcatCommandEntry> m_GeneralCommands;
        Action<AndroidLogcatCommandEntry> m_OnAddCommand;
        Action<AndroidLogcatCommandEntry> m_OnRunCommand;

        string m_SearchQuery = "";

        TextField m_SearchTextField;
        HelpBox m_EmptyMessage;
        VisualElement m_ResultsContainer;
        ScrollView m_ResultsScroll;

        static readonly string[] s_ChipTerms =
            { "devices", "packages", "logcat", "permissions", "input", "screen", "network", "dumpsys", "quest" };

        static readonly Color kCommandColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color kSavedSourceColor = new Color(0.4f, 0.7f, 0.4f);
        static readonly Color kCatalogSourceColor = new Color(0.5f, 0.6f, 0.9f);

        internal static void Open(
            List<AndroidLogcatCommandEntry> favorites,
            List<AndroidLogcatCommandEntry> generalCommands,
            Action<AndroidLogcatCommandEntry> onAdd,
            Action<AndroidLogcatCommandEntry> onRun)
        {
            var wnd = GetWindow<AndroidLogcatCommandSearchWindow>();
            wnd.titleContent = new GUIContent("Search Commands");
            wnd.minSize = new Vector2(500, 400);
            s_Instance = wnd;
            wnd.m_Favorites = favorites;
            wnd.m_GeneralCommands = generalCommands;
            wnd.m_OnAddCommand = onAdd;
            wnd.m_OnRunCommand = onRun;
            wnd.RebuildResults();
        }

        void OnEnable()
        {
            s_Instance = this;
            LoadUI();
        }

        void LoadUI()
        {
            var r = rootVisualElement;
            var tree = AndroidLogcatUtilities.LoadUXML("AndroidLogcatCommandSearch.uxml");
            tree.CloneTree(r);

            m_SearchTextField = r.Q<TextField>("SearchField");
            m_EmptyMessage = r.Q<HelpBox>("EmptyMessage");
            m_ResultsContainer = r.Q<VisualElement>("ResultsContainer");
            m_ResultsScroll = r.Q<ScrollView>("ResultsScroll");

            m_SearchTextField.value = m_SearchQuery;
            m_SearchTextField.RegisterValueChangedCallback(evt => { m_SearchQuery = evt.newValue; RebuildResults(); });

            var chipsRow = r.Q<VisualElement>("ChipsRow");
            foreach (var term in s_ChipTerms)
            {
                var chip = new Button(() => m_SearchTextField.value = term) { text = term };
                chip.style.marginRight = 2;
                chipsRow.Add(chip);
            }

            RebuildResults();
        }

        void RebuildResults()
        {
            if (m_ResultsContainer == null)
                return;

            m_ResultsContainer.Clear();

            if (string.IsNullOrWhiteSpace(m_SearchQuery))
            {
                SetEmptyState("Type a search term or click a chip to find commands.");
                return;
            }

            var terms = m_SearchQuery.Trim().ToLowerInvariant();
            var favorites = m_Favorites ?? new List<AndroidLogcatCommandEntry>();
            var general = m_GeneralCommands ?? new List<AndroidLogcatCommandEntry>();

            foreach (var e in favorites)
                if (MatchesSearch(e, terms))
                    m_ResultsContainer.Add(CreateResultRow(e, "Favorite", true));
            foreach (var e in general)
                if (MatchesSearch(e, terms))
                    m_ResultsContainer.Add(CreateResultRow(e, "General", true));

            var savedCommands = new HashSet<string>(
                favorites.Concat(general).Select(c => c.command), StringComparer.OrdinalIgnoreCase);
            foreach (var e in AndroidLogcatAdbCommandCatalog.All)
                if (MatchesSearch(e, terms) && !savedCommands.Contains(e.command))
                    m_ResultsContainer.Add(CreateResultRow(e, "Catalog", false));

            if (m_ResultsContainer.childCount == 0)
                SetEmptyState("No matching commands found.");
            else
            {
                m_EmptyMessage.style.display = DisplayStyle.None;
                m_ResultsScroll.style.display = DisplayStyle.Flex;
            }
        }

        void SetEmptyState(string message)
        {
            m_EmptyMessage.text = message;
            m_EmptyMessage.style.display = DisplayStyle.Flex;
            m_ResultsScroll.style.display = DisplayStyle.None;
        }

        VisualElement CreateResultRow(AndroidLogcatCommandEntry entry, string source, bool isSaved)
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

            var nameLabel = new Label(entry.name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.width = 180;
            nameLabel.style.minWidth = 180;
            row.Add(nameLabel);

            var cmdLabel = new Label(entry.command);
            cmdLabel.style.color = new StyleColor(kCommandColor);
            cmdLabel.style.flexGrow = 1;
            cmdLabel.style.overflow = Overflow.Hidden;
            row.Add(cmdLabel);

            var sourceLabel = new Label(source);
            sourceLabel.style.width = 55;
            sourceLabel.style.fontSize = 10;
            sourceLabel.style.color = new StyleColor(isSaved ? kSavedSourceColor : kCatalogSourceColor);
            row.Add(sourceLabel);

            if (!isSaved)
            {
                var addBtn = new Button(() => m_OnAddCommand?.Invoke(new AndroidLogcatCommandEntry(entry.name, entry.command))) { text = "Add" };
                addBtn.style.width = 40;
                row.Add(addBtn);
            }

            var runBtn = new Button(() => m_OnRunCommand?.Invoke(entry)) { text = "Run" };
            runBtn.style.width = 40;
            row.Add(runBtn);

            return row;
        }

        static bool MatchesSearch(AndroidLogcatCommandEntry entry, string terms)
        {
            return entry.name.ToLowerInvariant().Contains(terms)
                || entry.command.ToLowerInvariant().Contains(terms);
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }
    }
}
