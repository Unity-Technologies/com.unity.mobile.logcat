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
        private List<int> m_SelectedTags = new List<int>(new[] { kInvalidTagIndex, kAnyTagIndex, kInvalidTagIndex });

        private const byte kFilterByAllTags = 0;
        private const byte kAnyTagIndex = 1;
        private const byte kIndexOfFirstValidTag = 3; // Skipt the first 2 options + separator
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

            // Indexes
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

            if (tagIndex < kIndexOfFirstValidTag)
                return false;

            if (updateSelection && IsSelected(tagIndex))
            {
                TagSelected(null, null, tagIndex); // unselect it
                TagSelected(null, null, kAnyTagIndex); // Select *Any Tag*
            }

            newTags.Remove(tag);

            m_TagNames = newTags;

            // Selected indexes
            m_SelectedTags.RemoveAt(tagIndex);

            return true;
        }

        public string[] GetSelectedTags()
        {
            if (m_SelectedTags[kAnyTagIndex] == kAnyTagIndex)
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

        private void TagSelected(object userData, string[] options, int selected)
        {
            if (selected == kFilterByAllTags)
            {
                // unselect *any tag* and select all of others...s
                UpdateTagFilterBasedOnAnyTagOption(false);
            }
            else if (selected == kAnyTagIndex)
            {
                if (m_SelectedTags[kAnyTagIndex] == kInvalidTagIndex)
                {
                    // Select *any tag*, unselect all others
                    UpdateTagFilterBasedOnAnyTagOption(true);
                }
                else
                {
                    // Unselect *any tag*, select all others...
                    UpdateTagFilterBasedOnAnyTagOption(false);
                }
            }
            else
            {
                m_SelectedTags[selected] = m_SelectedTags[selected] == kInvalidTagIndex ? selected : kInvalidTagIndex;
                m_SelectedTags[kAnyTagIndex] = kInvalidTagIndex;
            }

            if (TagSelectionChanged != null)
                TagSelectionChanged.Invoke();
        }

        private void UpdateTagFilterBasedOnAnyTagOption(bool selectAnyTagEntry)
        {
            for (int i = kIndexOfFirstValidTag; i < m_SelectedTags.Count; i++)
                m_SelectedTags[i] = selectAnyTagEntry ? kInvalidTagIndex : i;

            m_SelectedTags[kAnyTagIndex] = selectAnyTagEntry ? kAnyTagIndex : kInvalidTagIndex;
        }

        private bool IsSelected(int index)
        {
            return m_SelectedTags[index] == index;
        }
    }
}
#endif
