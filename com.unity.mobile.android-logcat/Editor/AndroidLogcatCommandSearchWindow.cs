using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
        Vector2 m_ScrollPos;

        static readonly string[] s_ChipTerms =
            { "devices", "packages", "logcat", "permissions", "input", "screen", "network", "dumpsys", "quest" };

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
        }

        void OnGUI()
        {
            EditorGUILayout.Space(4);

            // Search field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            GUI.SetNextControlName("SearchField");
            m_SearchQuery = EditorGUILayout.TextField(m_SearchQuery);
            EditorGUILayout.EndHorizontal();

            // Quick-search chips
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Try:", GUILayout.Width(30));
            foreach (var term in s_ChipTerms)
            {
                if (GUILayout.Button(term, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    m_SearchQuery = term;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (string.IsNullOrWhiteSpace(m_SearchQuery))
            {
                EditorGUILayout.HelpBox("Type a search term or click a chip to find commands.", MessageType.Info);
                return;
            }

            var terms = m_SearchQuery.Trim().ToLowerInvariant();
            var favorites = m_Favorites ?? new List<AndroidLogcatCommandEntry>();
            var general = m_GeneralCommands ?? new List<AndroidLogcatCommandEntry>();

            // Collect local results
            var localResults = new List<(AndroidLogcatCommandEntry entry, string source)>();
            foreach (var e in favorites)
                if (MatchesSearch(e, terms))
                    localResults.Add((e, "Favorite"));
            foreach (var e in general)
                if (MatchesSearch(e, terms))
                    localResults.Add((e, "General"));

            // Collect catalog results (excluding already-saved commands)
            var savedCommands = new HashSet<string>(
                favorites.Concat(general).Select(c => c.command),
                StringComparer.OrdinalIgnoreCase);

            var catalogResults = new List<AndroidLogcatCommandEntry>();
            foreach (var e in AndroidLogcatAdbCommandCatalog.All)
                if (MatchesSearch(e, terms) && !savedCommands.Contains(e.command))
                    catalogResults.Add(e);

            if (localResults.Count == 0 && catalogResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching commands found.", MessageType.Info);
                return;
            }

            // Results list
            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

            foreach (var (entry, source) in localResults)
                DrawResultRow(entry, source, true);

            if (localResults.Count > 0 && catalogResults.Count > 0)
                EditorGUILayout.Space(4);

            foreach (var entry in catalogResults)
                DrawResultRow(entry, "Catalog", false);

            EditorGUILayout.EndScrollView();
        }

        void DrawResultRow(AndroidLogcatCommandEntry entry, string source, bool isSaved)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Name
            EditorGUILayout.LabelField(entry.name, EditorStyles.boldLabel, GUILayout.Width(180));

            // Command preview
            var style = new GUIStyle(EditorStyles.label) { wordWrap = false };
            style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField(entry.command, style);

            // Source label
            var sourceStyle = new GUIStyle(EditorStyles.miniLabel);
            sourceStyle.normal.textColor = isSaved
                ? new Color(0.4f, 0.7f, 0.4f)
                : new Color(0.5f, 0.6f, 0.9f);
            GUILayout.Label(source, sourceStyle, GUILayout.Width(55));

            // Add button (only for catalog entries not yet saved)
            if (!isSaved)
            {
                if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(40)))
                    m_OnAddCommand?.Invoke(new AndroidLogcatCommandEntry(entry.name, entry.command));
            }

            // Run button
            if (GUILayout.Button("Run", EditorStyles.miniButton, GUILayout.Width(40)))
                m_OnRunCommand?.Invoke(entry);

            EditorGUILayout.EndHorizontal();
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
