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
        List<AndroidLogcatCommandEntry> m_Favorites;
        List<AndroidLogcatCommandEntry> m_GeneralCommands;
        Action<AndroidLogcatCommandEntry> m_OnAddCommand;
        Action<AndroidLogcatCommandEntry> m_OnRunCommand;

        string m_SearchQuery = "";
        AndroidLogcatCommandCategory? m_CategoryFilter;

        TextField m_SearchTextField;
        HelpBox m_EmptyMessage;
        VisualElement m_ResultsContainer;
        ScrollView m_ResultsScroll;
        VisualElement m_ChipsRow;

        static AndroidLogcatCommandCategory[] GetPopulatedCategories()
        {
            return AndroidLogcatAdbCommandCatalog.All
                .Select(e => e.category)
                .Distinct()
                .OrderBy(c => (int)c)
                .ToArray();
        }

        static readonly Color kSavedSourceColor = new Color(0.4f, 0.7f, 0.4f);
        static readonly Color kCatalogSourceColor = new Color(0.5f, 0.6f, 0.9f);
        static readonly Color kActiveChipColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);
        static Color SectionHeaderColor =>
            EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.08f);

        const int kSourceColumnWidth = 55;
        const int kAddButtonWidth = 40;
        const int kRunButtonWidth = 40;

        internal static void Open(
            List<AndroidLogcatCommandEntry> favorites,
            List<AndroidLogcatCommandEntry> generalCommands,
            Action<AndroidLogcatCommandEntry> onAdd,
            Action<AndroidLogcatCommandEntry> onRun)
        {
            var wnd = GetWindow<AndroidLogcatCommandSearchWindow>();
            wnd.titleContent = new GUIContent("Search Commands");
            wnd.minSize = new Vector2(500, 400);
            wnd.m_Favorites = favorites;
            wnd.m_GeneralCommands = generalCommands;
            wnd.m_OnAddCommand = onAdd;
            wnd.m_OnRunCommand = onRun;
            wnd.RebuildResults();
        }

        void CreateGUI()
        {
            var r = rootVisualElement;
            r.Clear();

            var tree = AndroidLogcatUtilities.LoadUXML("Command/AndroidLogcatCommandSearch.uxml");
            tree.CloneTree(r);

            m_SearchTextField = r.Q<TextField>("SearchField");
            m_EmptyMessage = r.Q<HelpBox>("EmptyMessage");
            m_ResultsContainer = r.Q<VisualElement>("ResultsContainer");
            m_ResultsScroll = r.Q<ScrollView>("ResultsScroll");
            m_ChipsRow = r.Q<VisualElement>("ChipsRow");

            m_SearchTextField.value = m_SearchQuery;
            m_SearchTextField.RegisterValueChangedCallback(evt =>
            {
                m_SearchQuery = evt.newValue;
                RebuildResults();
            });

            RebuildChips();
            RebuildResults();
        }

        void RebuildChips()
        {
            if (m_ChipsRow == null)
                return;

            m_ChipsRow.Clear();
            m_ChipsRow.style.flexWrap = Wrap.Wrap;

            AddChip("All", null);
            foreach (var category in GetPopulatedCategories())
                AddChip(AndroidLogcatCommandMatcher.GetCategoryDisplayName(category), category);
        }

        void AddChip(string label, AndroidLogcatCommandCategory? category)
        {
            var chip = new Button(() =>
            {
                m_CategoryFilter = category;
                RebuildChips();
                RebuildResults();
            })
            { text = label };

            chip.style.marginRight = 2;
            chip.style.marginBottom = 2;
            chip.style.flexShrink = 0;

            var isActive = m_CategoryFilter.HasValue == category.HasValue &&
                (!category.HasValue || m_CategoryFilter.Value == category.Value);
            if (isActive)
                chip.style.backgroundColor = new StyleColor(kActiveChipColor);

            m_ChipsRow.Add(chip);
        }

        void RebuildResults()
        {
            if (m_ResultsContainer == null)
                return;

            m_ResultsContainer.Clear();

            var favorites = m_Favorites ?? new List<AndroidLogcatCommandEntry>();
            var general = m_GeneralCommands ?? new List<AndroidLogcatCommandEntry>();

            var isFiltering = !string.IsNullOrWhiteSpace(m_SearchQuery) || m_CategoryFilter.HasValue;

            var savedRows = new List<VisualElement>();
            var shownInSavedSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (isFiltering)
            {
                foreach (var e in favorites)
                    if (AndroidLogcatCommandMatcher.MatchesUserCommand(e, m_CategoryFilter, m_SearchQuery))
                    {
                        savedRows.Add(CreateResultRow(e, "Favorite", true, false));
                        shownInSavedSection.Add(e.command);
                    }

                foreach (var e in general)
                    if (AndroidLogcatCommandMatcher.MatchesUserCommand(e, m_CategoryFilter, m_SearchQuery))
                    {
                        savedRows.Add(CreateResultRow(e, "General", true, false));
                        shownInSavedSection.Add(e.command);
                    }
            }

            var savedCommands = new HashSet<string>(
                favorites.Concat(general).Where(c => c != null && !string.IsNullOrEmpty(c.command)).Select(c => c.command),
                StringComparer.OrdinalIgnoreCase);

            var catalogRows = new List<VisualElement>();
            foreach (var e in AndroidLogcatAdbCommandCatalog.All)
            {
                if (!AndroidLogcatCommandMatcher.Matches(e, m_CategoryFilter, m_SearchQuery))
                    continue;

                if (shownInSavedSection.Contains(e.command))
                    continue;

                catalogRows.Add(CreateResultRow(e, "Catalog", false, savedCommands.Contains(e.command)));
            }

            var showHeaders = savedRows.Count > 0 && catalogRows.Count > 0;

            if (savedRows.Count > 0)
            {
                if (showHeaders)
                    m_ResultsContainer.Add(CreateSectionHeader("Your commands"));
                foreach (var row in savedRows)
                    m_ResultsContainer.Add(row);
            }

            if (catalogRows.Count > 0)
            {
                if (showHeaders)
                    m_ResultsContainer.Add(CreateSectionHeader("Catalog"));
                foreach (var row in catalogRows)
                    m_ResultsContainer.Add(row);
            }

            if (m_ResultsContainer.childCount == 0)
            {
                SetEmptyState(string.IsNullOrWhiteSpace(m_SearchQuery)
                    ? "No commands in this category."
                    : "No matching commands found.");
            }
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

        static VisualElement CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 10;
            header.style.paddingLeft = 4;
            header.style.paddingTop = 2;
            header.style.paddingBottom = 2;
            header.style.flexShrink = 0;
            header.style.backgroundColor = new StyleColor(SectionHeaderColor);
            return header;
        }

        VisualElement CreateResultRow(AndroidLogcatCommandEntry entry, string source, bool isSaved, bool alreadyAdded)
        {
            var row = AndroidLogcatCommandUI.CreateRow();

            row.Add(AndroidLogcatCommandUI.CreateNameLabel(entry.name));

            var cmdLabel = AndroidLogcatCommandUI.CreateCommandLabel(entry.command);
            AndroidLogcatCommandUI.SetPointerTooltip(cmdLabel, entry.command);
            row.Add(cmdLabel);

            var sourceLabel = new Label(alreadyAdded ? "Added" : source);
            sourceLabel.style.width = kSourceColumnWidth;
            sourceLabel.style.minWidth = kSourceColumnWidth;
            sourceLabel.style.flexShrink = 0;
            sourceLabel.style.fontSize = 10;
            sourceLabel.style.color = new StyleColor(isSaved || alreadyAdded ? kSavedSourceColor : kCatalogSourceColor);
            if (alreadyAdded)
                sourceLabel.tooltip = "Already in your commands";
            row.Add(sourceLabel);

            if (isSaved)
            {
                AndroidLogcatCommandUI.AddSpacer(row, kAddButtonWidth);
            }
            else
            {
                AndroidLogcatCommandUI.AddRowButton(row, "Add", kAddButtonWidth,
                    () =>
                    {
                        m_OnAddCommand?.Invoke(entry.Clone());
                        rootVisualElement.schedule.Execute(RebuildResults);
                    },
                    !alreadyAdded,
                    alreadyAdded ? "Already in your commands" : "Add to your commands");
            }

            AndroidLogcatCommandUI.AddRowButton(row, "Run", kRunButtonWidth, () => m_OnRunCommand?.Invoke(entry));

            return row;
        }
    }
}
