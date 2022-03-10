using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal class AutoScrollInfo
    {
        internal enum AutoScroll
        {
            None,
            ScrollToEnd,
            ScrollToSelectedItem
        }

        public AutoScroll Type { get; }

        public int UserData { get; }

        public AutoScrollInfo(AutoScroll scroll)
        {
            Type = scroll;
        }

        public AutoScrollInfo(AutoScroll scroll, int userData)
        {
            Type = scroll;
            UserData = userData;
        }

        public static AutoScrollInfo None => new AutoScrollInfo(AutoScroll.None);
        public static AutoScrollInfo ScrollToEnd => new AutoScrollInfo(AutoScroll.ScrollToEnd);
    }
}
