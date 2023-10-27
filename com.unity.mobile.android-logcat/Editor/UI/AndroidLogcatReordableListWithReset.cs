using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatReordableListWithReset : AndroidLogcatReordableList
    {
        private Action m_ResetList;

        public AndroidLogcatReordableListWithReset(List<ReordableListItem> dataSource, Action resetList)
            : base(dataSource)
        {
            ShowResetGUI = true;
            m_ResetList = resetList;
        }

        protected override void OnResetButtonClicked()
        {
            m_ResetList();
        }

        protected override string ValidateItem(string item)
        {
            if (string.IsNullOrEmpty(item))
                return string.Empty;

            try
            {
                Regex.Match("", item);
            }
            catch (ArgumentException ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }
    }
}
