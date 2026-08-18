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

        /// <summary>
        /// Categories offered as filter chips. Built from the catalog so a category can never appear
        /// as an empty chip, which is what happened previously with the hardcoded term list.
        /// </summary>
        static AndroidLogcatCommandCategory[] GetPopulatedCategories()
        {
            return AndroidLogcatAdbCommandCatalog.All
                .Select(e => e.category)
                .Distinct()
                .OrderBy(c => (int)c)
                .ToArray();
        }

        static readonly Color kCommandColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color kSavedSourceColor = new Color(0.4f, 0.7f, 0.4f);
        static readonly Color kCatalogSourceColor = new Color(0.5f, 0.6f, 0.9f);
        static readonly Color kActiveChipColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);

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

            // Saved commands are matched on the search term only; the category filter applies to the
            // catalog, since user commands are frequently uncategorized.
            if (!string.IsNullOrWhiteSpace(m_SearchQuery))
            {
                foreach (var e in favorites)
                    if (AndroidLogcatCommandMatcher.Matches(e, m_SearchQuery))
                        m_ResultsContainer.Add(CreateResultRow(e, "Favorite", true));

                foreach (var e in general)
                    if (AndroidLogcatCommandMatcher.Matches(e, m_SearchQuery))
                        m_ResultsContainer.Add(CreateResultRow(e, "General", true));
            }

            var savedCommands = new HashSet<string>(
                favorites.Concat(general).Where(c => c != null && !string.IsNullOrEmpty(c.command)).Select(c => c.command),
                StringComparer.OrdinalIgnoreCase);

            foreach (var e in AndroidLogcatAdbCommandCatalog.All)
            {
                if (!AndroidLogcatCommandMatcher.Matches(e, m_CategoryFilter, m_SearchQuery))
                    continue;
                if (savedCommands.Contains(e.command))
                    continue;
                m_ResultsContainer.Add(CreateResultRow(e, "Catalog", false));
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
            cmdLabel.tooltip = entry.command;
            row.Add(cmdLabel);

            var sourceLabel = new Label(source);
            sourceLabel.style.width = 55;
            sourceLabel.style.fontSize = 10;
            sourceLabel.style.color = new StyleColor(isSaved ? kSavedSourceColor : kCatalogSourceColor);
            row.Add(sourceLabel);

            if (!isSaved)
            {
                var addBtn = new Button(() => m_OnAddCommand?.Invoke(entry.Clone())) { text = "Add" };
                addBtn.style.width = 40;
                row.Add(addBtn);
            }

            var runBtn = new Button(() => m_OnRunCommand?.Invoke(entry)) { text = "Run" };
            runBtn.style.width = 40;
            row.Add(runBtn);

            return row;
        }
    }
}
