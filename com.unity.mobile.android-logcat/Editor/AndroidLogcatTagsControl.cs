using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
#if PLATFORM_ANDROID
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

        private AndroidLogcatTagWindow m_TagWindow = null;
        public AndroidLogcatTagWindow TagWindow
        {
            get { return m_TagWindow; }
            set { m_TagWindow = value; }
        }

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

        public void DoGUI(Rect rect)
        {
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
            bool tagWindowSelected = false;
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
                tagWindowSelected = true;
                m_TagWindow = AndroidLogcatTagWindow.Show(this);
            }
            else
            {
                m_SelectedTags[selectedIndex] = !m_SelectedTags[selectedIndex];
                m_SelectedTags[(int)AndroidLogcatTagType.NoFilter] = !(GetSelectedTags(true).Length > 0);
            }

            if (tagWindowSelected)
                return;

            if (m_TagWindow != null)
                m_TagWindow.Repaint();

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

    internal class AndroidLogcatTagWindow : EditorWindow
    {
        private AndroidLogcatTagsControl m_TagControl = null;
        private int m_SelectedTagIndex = -1;
        private string m_InputTagName = String.Empty;
        private const string kTagInputTextFieldControlId = "TagInputTextFieldControl";

        public Vector2 m_ScrollPosition = Vector2.zero;

        private static AndroidLogcatTagWindow s_TagWindow = null;

        public static AndroidLogcatTagWindow Show(AndroidLogcatTagsControl tagControl)
        {
            if (s_TagWindow == null)
                s_TagWindow = ScriptableObject.CreateInstance<AndroidLogcatTagWindow>();

            s_TagWindow.m_TagControl = tagControl;
            s_TagWindow.titleContent = new GUIContent("Tag Control");
            s_TagWindow.Show();
            s_TagWindow.Focus();

            return s_TagWindow;
        }

        void OnDestroy()
        {
            m_TagControl.TagWindow = null;
        }

        void OnGUI()
        {
            var currentEvent = Event.current;
            bool hitEnter = currentEvent.type == EventType.KeyDown && (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter);

            const float kEntryMargin = 8;
            EditorGUILayout.BeginVertical();
            GUILayout.Space(kEntryMargin);

            // Draw the input field & "Add" Button.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(kEntryMargin + 7);
            GUI.SetNextControlName(kTagInputTextFieldControlId);
            m_InputTagName = EditorGUILayout.TextField(m_InputTagName, GUILayout.Height(AndroidLogcatStyles.kTagEntryFixedHeight + 2));
            if (m_InputTagName.Length > 23)
            {
                GUILayout.Space(kEntryMargin + 2);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(kEntryMargin + 7);
                EditorGUILayout.HelpBox("The logging tag can be at most 23 characters, was " + m_InputTagName.Length + " .", MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("Add", GUILayout.Width(40)) || (hitEnter && GUI.GetNameOfFocusedControl() == kTagInputTextFieldControlId))
                {
                    if (!string.IsNullOrEmpty(m_InputTagName))
                    {
                        m_TagControl.Add(m_InputTagName);
                        m_InputTagName = string.Empty;
                        GUIUtility.keyboardControl = 0; // Have to remove the focus from the input text field to clear it.
                    }
                }
            }
            GUILayout.Space(kEntryMargin + 2);
            EditorGUILayout.EndHorizontal();

            // Get the visible window rect and tag window rect for scroll view.
            var visibleWindowRect = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.tagEntryStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var tagsHeight = (AndroidLogcatStyles.kTagEntryFixedHeight) * (m_TagControl.TagNames.Count - (int)AndroidLogcatTagType.FirstValidTag);
            var tagWindowRect = visibleWindowRect;
            tagWindowRect.width = visibleWindowRect.width - 10;
            tagWindowRect.height = tagsHeight + 2 * kEntryMargin;

            // Draw scroll view without horizontal scrollbar.
            m_ScrollPosition = GUI.BeginScrollView(visibleWindowRect, m_ScrollPosition, tagWindowRect, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            // Set the rects for all the UI widgets, loop y for each tag.
            var backgroundRect = new Rect(2 * kEntryMargin, 0, tagWindowRect.width - kEntryMargin - 10, AndroidLogcatStyles.kTagEntryFixedHeight);
            var tagLabelRect = new Rect(2 * kEntryMargin + 2, 0, tagWindowRect.width - 2 * AndroidLogcatStyles.ktagToggleFixedWidth - 3 * kEntryMargin - 10, AndroidLogcatStyles.kTagEntryFixedHeight);
            var toggleRect = new Rect(tagLabelRect.width + 20, 0, AndroidLogcatStyles.ktagToggleFixedWidth, AndroidLogcatStyles.kTagEntryFixedHeight);
            var removeButtonRect = new Rect(toggleRect.x + AndroidLogcatStyles.ktagToggleFixedWidth + kEntryMargin, 0, AndroidLogcatStyles.ktagToggleFixedWidth, AndroidLogcatStyles.kTagEntryFixedHeight);
            var removeTextRect = new Rect(removeButtonRect.x + 2, 0, removeButtonRect.width, removeButtonRect.height);

            // Draw tag list.
            float yOffset = tagWindowRect.y + kEntryMargin;
            var tagNames = m_TagControl.TagNames;
            var selectedTags = m_TagControl.SelectedTags;
            for (int i = (int)AndroidLogcatTagType.FirstValidTag; i < m_TagControl.TagNames.Count; ++i)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(kEntryMargin);

                tagLabelRect.y = yOffset;
                backgroundRect.y = yOffset;
                if (currentEvent.type == EventType.Repaint)
                {
                    if (m_SelectedTagIndex == i)
                        AndroidLogcatStyles.tagEntryBackground.Draw(tagLabelRect, false, false, true, false);
                    else
                    {
                        if (i % 2 == 0)
                            AndroidLogcatStyles.tagEntryBackgroundEven.Draw(backgroundRect, false, false, false, false);
                        else
                            AndroidLogcatStyles.tagEntryBackgroundOdd.Draw(backgroundRect, false, false, false, false);
                    }

                    AndroidLogcatStyles.tagEntryStyle.Draw(tagLabelRect, new GUIContent(tagNames[i]), 0);
                }
                else
                {
                    DoMouseEvent(tagLabelRect, i);
                }

                // Draw the toggle.
                toggleRect.y = yOffset;
                var toggled = GUI.Toggle(toggleRect, selectedTags[i], String.Empty, AndroidLogcatStyles.tagToggleStyle);
                if (toggled != selectedTags[i])
                {
                    m_TagControl.TagSelected(null, null, i);
                    GUIUtility.keyboardControl = 0;
                }

                // Draw the remove button.
                GUILayout.Space(kEntryMargin);
                removeButtonRect.y = yOffset;
                if (GUI.Button(removeButtonRect, string.Empty, AndroidLogcatStyles.tagToggleStyle))
                {
                    RemoveSelected(i);
                    GUIUtility.keyboardControl = 0;
                }
                removeTextRect.y = yOffset + 1;
                GUI.Label(removeTextRect, "X", AndroidLogcatStyles.removeTextStyle);

                GUILayout.Space(kEntryMargin);
                EditorGUILayout.EndHorizontal();

                yOffset += AndroidLogcatStyles.kTagEntryFixedHeight;
            }

            // Draw the borders.
            var orgColor = GUI.color;
            GUI.color = Color.black;
            yOffset = tagWindowRect.y + kEntryMargin;
            GUI.DrawTexture(new Rect(kEntryMargin + 6, yOffset - 4, 1, tagsHeight + 8), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(kEntryMargin + 6, yOffset - 4, tagWindowRect.width - kEntryMargin - 3, 1), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(tagWindowRect.width + 2, yOffset - 4, 1, tagsHeight + 8), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(kEntryMargin + 6, yOffset + tagsHeight + 4, tagWindowRect.width - kEntryMargin - 3, 1), EditorGUIUtility.whiteTexture);
            GUI.color = orgColor;

            GUI.EndScrollView();

            GUILayout.Space(kEntryMargin);
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
#else
    internal class AndroidLogcatTagWindow : EditorWindow
    {
        internal void OnGUI()
        {
            EditorGUILayout.HelpBox("Please switch active platform to be Android in Build Settings Window.", MessageType.Info);
        }
    }
#endif
}
