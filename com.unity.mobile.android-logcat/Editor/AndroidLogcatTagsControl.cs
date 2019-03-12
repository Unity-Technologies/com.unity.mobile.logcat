#if PLATFORM_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatTagsControl
    {
        [SerializeField]
        private List<string> m_TagNames = new List<string>(new[] { "Filter by all listed tags", "No Filter", null });

        [SerializeField]
        private List<bool> m_SelectedTags = new List<bool>(new[] { false, true, false });

        private const byte kAllTagsIndex = 0;
        private const byte kNoFilterIndex = 1;
        private const byte kFirstValidTagIndex = 3; // Skip the first 2 options + separator

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
            if (tagIndex < kFirstValidTagIndex)
                return false;

            if (IsSelected(tagIndex))
                TagSelected(null, null, tagIndex); // Deselect it

            m_TagNames.Remove(tag);
            m_SelectedTags.RemoveAt(tagIndex);

            return true;
        }

        public string[] GetSelectedTags(bool skipNoFilterIndex = false)
        {
            if (!skipNoFilterIndex && m_SelectedTags[kNoFilterIndex])
                return new string[0];

            var selectedTagNames = new List<string>(m_SelectedTags.Count);
            for (int i = kFirstValidTagIndex; i < m_SelectedTags.Count; i++)
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

        private void TagSelected(object userData, string[] options, int selectedIndex)
        {
            if (selectedIndex == kAllTagsIndex)
            {
                // Deselect *No Filter* and select all others.
                UpdateTagFilterBasedOnNoFilterOption(false);
            }
            else if (selectedIndex == kNoFilterIndex)
            {
                if (!m_SelectedTags[kNoFilterIndex])
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
            else
            {
                m_SelectedTags[selectedIndex] = !m_SelectedTags[selectedIndex];
                m_SelectedTags[kNoFilterIndex] = !(GetSelectedTags(true).Length > 0);
            }

            if (TagSelectionChanged != null)
                TagSelectionChanged.Invoke();
        }

        private void UpdateTagFilterBasedOnNoFilterOption(bool isNoFilterSelected)
        {
            m_SelectedTags[kNoFilterIndex] = isNoFilterSelected;

            for (int i = kFirstValidTagIndex; i < m_SelectedTags.Count; i++)
                m_SelectedTags[i] = !isNoFilterSelected;
        }

        private bool IsSelected(int tagIndex)
        {
            return m_SelectedTags[tagIndex];
        }
    }
}
#endif
