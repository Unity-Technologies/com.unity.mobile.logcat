#if PLATFORM_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal enum AndroidLogcatTagType
    {
        AllTags = 0,
        NoFilter = 1,
        TagControl = 3,
        FirstValidTag = 5 // Skip the options + separators
    }

    [Serializable]
    internal class AndroidLogcatTagsControl
    {
        [SerializeField]
        private List<string> m_TagNames = new List<string>(new[] { "Filter by all listed tags", "No Filter", null, "Tag Control...", null, "Unity", "CRASH" });
        public List<string> TagNames
        {
            get { return m_TagNames; }
            set { m_TagNames = value; }
        }

        [SerializeField]
        private List<bool> m_SelectedTags = new List<bool>(new[] { false, true, false, false, false, false, false });

        public List<bool> SelectedTags
        {
            get { return m_SelectedTags; }
            set { m_SelectedTags = value; }
        }

        public event Action TagSelectionChanged;

        private Rect m_TagButtonRect = new Rect();

        public AndroidLogcatTagsControl()
        {
        }

        public bool Add(string tag, bool isSelected = false)
        {
            if (string.IsNullOrEmpty(tag) || m_TagNames.IndexOf(tag) > 0)
                return false;

            m_TagNames.Add(tag);
            m_SelectedTags.Add(false);

            if (isSelected)
                TagSelected(null, null, m_SelectedTags.Count - 1); // This will set the selected state.

            return true;
        }

        public bool Remove(int tagIndex)
        {
            if (tagIndex < (int)AndroidLogcatTagType.FirstValidTag)
                return false;

            if (IsSelected(tagIndex))
                TagSelected(null, null, tagIndex); // Deselect it

            m_TagNames.Remove(m_TagNames[tagIndex]);
            m_SelectedTags.RemoveAt(tagIndex);

            return true;
        }

        public bool Remove(string tag)
        {
            var tagIndex = m_TagNames.IndexOf(tag);
            return Remove(tagIndex);
        }

        public string[] GetSelectedTags(bool skipNoFilterIndex = false)
        {
            if (!skipNoFilterIndex && m_SelectedTags[(int)AndroidLogcatTagType.NoFilter])
                return null;

            var selectedTagNames = new List<string>(m_SelectedTags.Count);
            for (int i = (int)AndroidLogcatTagType.FirstValidTag; i < m_SelectedTags.Count; i++)
            {
                if (m_SelectedTags[i])
                {
                    selectedTagNames.Add(m_TagNames[i]);
                }
            }

            return selectedTagNames.ToArray();
        }

        public void DoGUI(Rect rect, Rect tagButtonRect)
        {
            m_TagButtonRect = tagButtonRect;

            var separators = m_TagNames.Select(t => t == null).ToArray();
            var enabled = Enumerable.Repeat(true, m_TagNames.Count).ToArray();
            var selectedTags = new List<int>();
            for (int i = 0; i < m_SelectedTags.Count; ++i)
            {
                if (m_SelectedTags[i])
                    selectedTags.Add(i);
            }

            EditorUtility.DisplayCustomMenuWithSeparators(new Rect(rect.x, rect.y + rect.height, 0, 0), m_TagNames.ToArray(), enabled, separators, selectedTags.ToArray(), TagSelected, null);
        }

        public void TagSelected(object userData, string[] options, int selectedIndex)
        {
            if (selectedIndex == (int)AndroidLogcatTagType.AllTags)
            {
                // Deselect *No Filter* and select all others.
                UpdateTagFilterBasedOnNoFilterOption(false);
            }
            else if (selectedIndex == (int)AndroidLogcatTagType.NoFilter)
            {
                if (!m_SelectedTags[(int)AndroidLogcatTagType.NoFilter])
                {
                    // Select *No Filter*, deselect all others.
                    UpdateTagFilterBasedOnNoFilterOption(true);
                }
                else
                {
                    // Deselect *No Filter*, select all others.
                    UpdateTagFilterBasedOnNoFilterOption(false);
                }
            }
            else if (selectedIndex == (int)AndroidLogcatTagType.TagControl)
            {
                PopupWindow.Show(new Rect(m_TagButtonRect.x + 2, m_TagButtonRect.y + m_TagButtonRect.height * 2, 0, 0), new AndroidLogcatTagListPopup(this));
                return;
            }
            else
            {
                m_SelectedTags[selectedIndex] = !m_SelectedTags[selectedIndex];
                m_SelectedTags[(int)AndroidLogcatTagType.NoFilter] = !(GetSelectedTags(true).Length > 0);
            }

            if (TagSelectionChanged != null)
                TagSelectionChanged.Invoke();
        }

        private void UpdateTagFilterBasedOnNoFilterOption(bool isNoFilterSelected)
        {
            m_SelectedTags[(int)AndroidLogcatTagType.NoFilter] = isNoFilterSelected;

            for (int i = (int)AndroidLogcatTagType.FirstValidTag; i < m_SelectedTags.Count; i++)
                m_SelectedTags[i] = !isNoFilterSelected;
        }

        private bool IsSelected(int tagIndex)
        {
            return m_SelectedTags[tagIndex];
        }
    }

    internal class AndroidLogcatTagListPopup : PopupWindowContent
    {
        private AndroidLogcatTagsControl m_TagControl = null;
        private int m_SelectedTagIndex = -1;
        private string m_InputTagName = String.Empty;
        private const string kTagInputTextFieldControlId = "TagInputTextFieldControl";
        private static GUIContent kIconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove from list");

        public Vector2 m_ScrollPosition = Vector2.zero;

        public AndroidLogcatTagListPopup(AndroidLogcatTagsControl tagsControl)
        {
            m_TagControl = tagsControl;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, 200);
        }

        void DoTagListGUI(float entryMargin)
        {
            var currentEvent = Event.current;
            var buttonWidth = 25;
            var tagNames = m_TagControl.TagNames;
            var selectedTags = m_TagControl.SelectedTags;
            GUILayout.BeginHorizontal();
            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandWidth(true));

            for (int i = (int)AndroidLogcatTagType.FirstValidTag; i < m_TagControl.TagNames.Count; ++i)
            {
                EditorGUILayout.BeginHorizontal();

                var labelStyle = AndroidLogcatStyles.tagEntryStyle;
                var toggleStyle = AndroidLogcatStyles.tagToggleStyle;
                var buttonStyle = AndroidLogcatStyles.tagButtonStyle;

                var labelRect = GUILayoutUtility.GetRect(new GUIContent(tagNames[i]), labelStyle);
                var toggleRect = GUILayoutUtility.GetRect(GUIContent.none, toggleStyle, GUILayout.Width(buttonWidth));
                var buttonRect = GUILayoutUtility.GetRect(kIconToolbarMinus, buttonStyle, GUILayout.Width(buttonWidth));

                var itemRect = new Rect(labelRect.x, labelRect.y, buttonRect.max.x - labelRect.min.x, buttonRect.max.y - labelRect.min.y);
                if (currentEvent.type == EventType.Repaint)
                {
                    if (m_SelectedTagIndex == i)
                        AndroidLogcatStyles.tagEntryBackground.Draw(itemRect, false, false, true, false);
                    else
                    {
                        if (i % 2 == 0)
                            AndroidLogcatStyles.tagEntryBackgroundEven.Draw(itemRect, false, false, false, false);
                        else
                            AndroidLogcatStyles.tagEntryBackgroundOdd.Draw(itemRect, false, false, false, false);
                    }
                }
                else
                {
                    var selectableRect = itemRect;
                    selectableRect.width = toggleRect.min.x - labelRect.min.x;
                    DoMouseEvent(selectableRect, i);
                }

                GUI.Label(labelRect, new GUIContent(tagNames[i]), labelStyle);
                var toggled = GUI.Toggle(toggleRect, selectedTags[i], String.Empty, toggleStyle);
                if (toggled != selectedTags[i])
                {
                    m_TagControl.TagSelected(null, null, i);
                    GUIUtility.keyboardControl = 0;
                }

                // Draw the remove button.
                if (GUI.Button(buttonRect, kIconToolbarMinus, buttonStyle))
                {
                    RemoveSelected(i);
                    GUIUtility.keyboardControl = 0;
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            var rc = GUILayoutUtility.GetLastRect();
            GUILayout.Space(4);
            GUILayout.EndHorizontal();
            GUI.Box(new Rect(rc.x + 4, rc.y, rc.width - 4, rc.height), GUIContent.none, EditorStyles.helpBox);
            GUILayout.Space(entryMargin);
        }

        public override void OnGUI(Rect rect)
        {
            var currentEvent = Event.current;
            bool hitEnter = currentEvent.type == EventType.KeyDown && (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter);

            const float kEntryMargin = 8;
            EditorGUILayout.BeginVertical();
            GUILayout.Space(kEntryMargin);

            // Draw the input field & "Add" Button.
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUI.SetNextControlName(kTagInputTextFieldControlId);
            m_InputTagName = EditorGUILayout.TextField(m_InputTagName, GUILayout.Height(AndroidLogcatStyles.kTagEntryFixedHeight + 2));
            var trimmedTagName = m_InputTagName.Trim();
            if (trimmedTagName.Length > 23)
            {
                GUILayout.Space(kEntryMargin + 2);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(kEntryMargin + 7);
                EditorGUILayout.HelpBox("The logging tag can be at most 23 characters, was " + trimmedTagName.Length + " .", MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("Add", GUILayout.Width(40)) || (hitEnter && GUI.GetNameOfFocusedControl() == kTagInputTextFieldControlId))
                {
                    if (!string.IsNullOrEmpty(trimmedTagName))
                    {
                        m_TagControl.Add(trimmedTagName);
                        m_InputTagName = string.Empty;
                        GUIUtility.keyboardControl = 0; // Have to remove the focus from the input text field to clear it.
                    }
                }
            }
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
            DoTagListGUI(kEntryMargin);
            EditorGUILayout.EndVertical();
        }

        private void DoMouseEvent(Rect rect, int tagIndex)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                switch (e.button)
                {
                    case 0:
                        m_SelectedTagIndex = (m_SelectedTagIndex == tagIndex) ? -1 : tagIndex;
                        e.Use();
                        GUIUtility.keyboardControl = 0;
                        break;
                }
            }
        }

        public bool RemoveSelected(int tagIndex)
        {
            if (tagIndex < 0 || tagIndex >= m_TagControl.TagNames.Count)
                return false;

            // Simply reset to no selected.
            m_SelectedTagIndex = -1;
            m_TagControl.Remove(tagIndex);

            return true;
        }
    }
}
#endif
