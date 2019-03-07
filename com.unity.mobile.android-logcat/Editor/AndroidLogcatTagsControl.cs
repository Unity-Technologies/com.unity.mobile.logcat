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
        private List<string> m_TagNames = new List<string>(new[] { "Filter by all listed tags", "No Filter", null, "Tag Control...", null });
        public List<string> TagNames { get { return m_TagNames; } }

        [SerializeField]
        private List<bool> m_SelectedTags = new List<bool>(new[] { false, true, false, false, false });
        public List<bool> SelectedTags { get { return m_SelectedTags; } }

        public event Action TagSelectionChanged;

        public AndroidLogcatTagsControl()
        {
        }

        public bool Add(string tag, bool isSelected = false)
        {
            if (m_TagNames.IndexOf(tag) > 0)
                return false;

            m_TagNames.Add(tag);
            m_SelectedTags.Add(false);

            if (isSelected)
                TagSelected(null, null, m_SelectedTags.Count - 1); // This will set the selected state.

            return true;
        }

        public bool Remove(string tag)
        {
            var tagIndex = m_TagNames.IndexOf(tag);
            if (tagIndex < (int)AndroidLogcatTagType.FirstValidTag)
                return false;

            if (IsSelected(tagIndex))
                TagSelected(null, null, tagIndex); // Deselect it

            m_TagNames.Remove(tag);
            m_SelectedTags.RemoveAt(tagIndex);

            return true;
        }

        public string[] GetSelectedTags(bool skipNoFilterIndex = false)
        {
            if (!skipNoFilterIndex && m_SelectedTags[(int)AndroidLogcatTagType.NoFilter])
                return new string[0];

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
                AndroidLogcatTagWindow.Show(this);
            }
            else
            {
                m_SelectedTags[selectedIndex] = !m_SelectedTags[selectedIndex];
                m_SelectedTags[(int)AndroidLogcatTagType.NoFilter] = !(GetSelectedTags(true).Length > 0);
            }

            if (!tagWindowSelected && TagSelectionChanged != null)
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

        private static AndroidLogcatTagWindow s_TagWindow = null;
        public static void Show(AndroidLogcatTagsControl tagControl)
        {
            if (s_TagWindow == null)
                s_TagWindow = ScriptableObject.CreateInstance<AndroidLogcatTagWindow>();
            s_TagWindow.m_TagControl = tagControl;
            s_TagWindow.titleContent = new GUIContent("Tag Control");
            s_TagWindow.Show();
            s_TagWindow.Focus();
        }

        void OnGUI()
        {
            // Get the window with no height to get the width.
            var noHeightWindowRect = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.priorityDefaultStyle, GUILayout.ExpandWidth(true), GUILayout.Height(0));

            EditorGUILayout.BeginVertical();
            GUILayout.Space(AndroidLogcatStyles.kTagEntryFontSize);

            var tagNames = m_TagControl.TagNames;
            var selectedTags = m_TagControl.SelectedTags;
            const float kEntryMargin = 8;

            var e = Event.current;
            for (int i = (int)AndroidLogcatTagType.FirstValidTag; i < tagNames.Count; ++i)
            {
                var selectionRect = new Rect(
                    kEntryMargin,
                    AndroidLogcatStyles.kTagEntryFontSize + 1 + (AndroidLogcatStyles.kTagEntryFixedHeight + 2) * (i - (int)AndroidLogcatTagType.FirstValidTag),
                    noHeightWindowRect.width - AndroidLogcatStyles.ktagToggleFixedWidth - 2 * kEntryMargin,
                    AndroidLogcatStyles.kTagEntryFixedHeight);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(kEntryMargin);
                if (e.type == EventType.Repaint)
                {
                    if (m_SelectedTagIndex == i)
                        AndroidLogcatStyles.background.Draw(selectionRect, false, false, true, false);

                    AndroidLogcatStyles.TagEntryStyle.Draw(selectionRect, new GUIContent(tagNames[i]), 0);
                }
                else
                {
                    DoMouseEvent(selectionRect, i);
                }

                var toggleRect = new Rect(selectionRect.width + 10, selectionRect.y, AndroidLogcatStyles.ktagToggleFixedWidth, selectionRect.height);
                var toggled = GUI.Toggle(toggleRect, selectedTags[i], String.Empty, AndroidLogcatStyles.TagToggleStyle);
                if (toggled != selectedTags[i])
                {
                    m_TagControl.TagSelected(null, null, i);
                }

                GUILayout.Space(kEntryMargin);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(AndroidLogcatStyles.kTagEntryFontSize);
            EditorGUILayout.EndVertical();
        }

        private bool DoMouseEvent(Rect tagRect, int tagIndex)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && tagRect.Contains(e.mousePosition))
            {
                switch (e.button)
                {
                    case 0:
                        m_SelectedTagIndex = (m_SelectedTagIndex == tagIndex) ? -1 : tagIndex;
                        e.Use();
                        break;
                }
            }

            return false;
        }
    }
}
#endif
