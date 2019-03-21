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
        private const string kInputTextFieldControlId = "InputTextFieldControl";

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
            bool needRepaint = false;
            var e = Event.current;
            bool hitEnter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);

            // Get the window with no height to get the width.
            var noHeightWindowRect = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.tagEntryStyle, GUILayout.ExpandWidth(true), GUILayout.Height(0));
            const float kEntryMargin = 8;

            EditorGUILayout.BeginVertical();
            GUILayout.Space(AndroidLogcatStyles.kTagEntryFontSize);

            var tagNames = new List<string>(m_TagControl.TagNames);
            var selectedTags = new List<bool>(m_TagControl.SelectedTags);
            for (int i = (int)AndroidLogcatTagType.FirstValidTag; i < tagNames.Count; ++i)
            {
                var tagLabelRect = new Rect(
                    kEntryMargin,
                    AndroidLogcatStyles.kTagEntryFontSize + (AndroidLogcatStyles.kTagEntryFixedHeight) * (i - (int)AndroidLogcatTagType.FirstValidTag),
                    noHeightWindowRect.width - 2 * AndroidLogcatStyles.ktagToggleFixedWidth - 3 * kEntryMargin,
                    AndroidLogcatStyles.kTagEntryFixedHeight);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(kEntryMargin);

                var backgroundRect = new Rect(tagLabelRect.x - 2, tagLabelRect.y, noHeightWindowRect.width - kEntryMargin, tagLabelRect.height);
                if (e.type == EventType.Repaint)
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
                var toggleRect = new Rect(tagLabelRect.width + 10, tagLabelRect.y, AndroidLogcatStyles.ktagToggleFixedWidth, tagLabelRect.height);
                var toggled = GUI.Toggle(toggleRect, selectedTags[i], String.Empty, AndroidLogcatStyles.tagToggleStyle);
                if (toggled != selectedTags[i])
                {
                    m_TagControl.TagSelected(null, null, i);
                }

                // Draw the remove button.
                GUILayout.Space(kEntryMargin);
                var removeButtonRect = new Rect(tagLabelRect.width + 10 + AndroidLogcatStyles.ktagToggleFixedWidth + kEntryMargin,
                    tagLabelRect.y, AndroidLogcatStyles.ktagToggleFixedWidth, tagLabelRect.height);
                if (GUI.Button(removeButtonRect, string.Empty, AndroidLogcatStyles.tagToggleStyle))
                {
                    needRepaint |= RemoveSelected(i);
                }
                var removeTextRect = new Rect(removeButtonRect.x + 2, removeButtonRect.y + 1, removeButtonRect.width, removeButtonRect.height);
                GUI.Label(removeTextRect, "X", AndroidLogcatStyles.removeTextStyle);

                GUILayout.Space(kEntryMargin);
                EditorGUILayout.EndHorizontal();
            }

            // Draw the borders.
            var drawnHeight = (AndroidLogcatStyles.kTagEntryFixedHeight) * (tagNames.Count - (int)AndroidLogcatTagType.FirstValidTag);
            var orgColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(kEntryMargin - 4, 2 * kEntryMargin - 8, 1, drawnHeight + 8), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(kEntryMargin - 4, 2 * kEntryMargin - 8, noHeightWindowRect.width - kEntryMargin + 6, 1), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(noHeightWindowRect.width + 2, 2 * kEntryMargin - 8, 1, drawnHeight + 8), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(kEntryMargin - 4, 2 * kEntryMargin + drawnHeight, noHeightWindowRect.width - kEntryMargin + 6, 1), EditorGUIUtility.whiteTexture);
            GUI.color = orgColor;

            GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.tagEntryStyle, GUILayout.ExpandWidth(true), GUILayout.Height(drawnHeight));
            GUILayout.Space(kEntryMargin);

            // Draw the input field.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(kEntryMargin - 3);
            GUI.SetNextControlName(kInputTextFieldControlId);
            m_InputTagName = EditorGUILayout.TextField(m_InputTagName, GUILayout.Height(AndroidLogcatStyles.kTagEntryFixedHeight + 2));
            if (m_InputTagName.Length > 23)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("The logging tag can be at most 23 characters, was " + m_InputTagName.Length + " .", MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("Add", GUILayout.Width(40)) || (hitEnter && GUI.GetNameOfFocusedControl() == kInputTextFieldControlId))
                {
                    if (!string.IsNullOrEmpty(m_InputTagName))
                    {
                        m_TagControl.Add(m_InputTagName);
                        m_InputTagName = string.Empty;
                    }
                }
                GUILayout.Space(2);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(AndroidLogcatStyles.kTagEntryFontSize);
            EditorGUILayout.EndVertical();

            if (needRepaint)
                Repaint();
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
