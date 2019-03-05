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
        private List<int> m_SelectedTags = new List<int>(new[] { kInvalidTagIndex, kNoFilterIndex, kInvalidTagIndex });

        private const byte kAllTagsIndex = 0;
        private const byte kNoFilterIndex = 1;
        private const byte kFirstValidTagIndex = 3; // Skip the first 2 options + separator
        private const int kInvalidTagIndex = Int32.MaxValue;

        public event Action TagSelectionChanged;

        public AndroidLogcatTagsControl()
        {
        }

        public bool Add(string tag, bool selected = false)
        {
            if (m_TagNames.Where(tagName => tagName == tag).FirstOrDefault() != null)
                return false;

            // Tag names
            m_TagNames.Add(tag);

            // Indices
            m_SelectedTags.Add(kInvalidTagIndex);

            if (selected)
                TagSelected(null, null, m_SelectedTags.Count - 1);
            return true;
        }

        public bool Remove(string tag, bool updateSelection = false)
        {
            if (m_TagNames.Where(tagName => tagName == tag).FirstOrDefault() != null)
                return false;

            // Tag names
            var newTags = new List<string>(m_TagNames);
            var tagIndex = newTags.IndexOf(tag);

            if (tagIndex < kFirstValidTagIndex)
                return false;

            if (updateSelection && IsSelected(tagIndex))
            {
                TagSelected(null, null, tagIndex); // Deselect it
                TagSelected(null, null, kNoFilterIndex); // Select *No Filter*
            }

            newTags.Remove(tag);

            m_TagNames = newTags;

            // Selected indices
            m_SelectedTags.RemoveAt(tagIndex);

            return true;
        }

        public string[] GetSelectedTags()
        {
            if (m_SelectedTags[kNoFilterIndex] == kNoFilterIndex)
                return new string[0];

            var selectedTagNames = new List<string>(m_SelectedTags.Count);
            for (int i = 0; i < m_SelectedTags.Count; i++)
            {
                if (m_SelectedTags[i] == i)
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

            EditorUtility.DisplayCustomMenuWithSeparators(new Rect(rect.x, rect.y + rect.height, 0, 0), m_TagNames.ToArray(), enabled, separators, m_SelectedTags.ToArray(), TagSelected, null);
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
                if (m_SelectedTags[kNoFilterIndex] == kInvalidTagIndex)
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
                m_SelectedTags[selectedIndex] = m_SelectedTags[selectedIndex] == kInvalidTagIndex ? selectedIndex : kInvalidTagIndex;
                m_SelectedTags[kNoFilterIndex] = kInvalidTagIndex;
            }

            if (TagSelectionChanged != null)
                TagSelectionChanged.Invoke();
        }

        private void UpdateTagFilterBasedOnNoFilterOption(bool isNoFilterEntrySelected)
        {
            for (int i = kFirstValidTagIndex; i < m_SelectedTags.Count; i++)
                m_SelectedTags[i] = isNoFilterEntrySelected ? kInvalidTagIndex : i;

            m_SelectedTags[kNoFilterIndex] = isNoFilterEntrySelected ? kNoFilterIndex : kInvalidTagIndex;
        }

        private bool IsSelected(int index)
        {
            return m_SelectedTags[index] == index;
        }
    }
}
#endif
