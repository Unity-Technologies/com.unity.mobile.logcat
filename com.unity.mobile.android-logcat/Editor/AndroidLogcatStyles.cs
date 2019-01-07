#if PLATFORM_ANDROID
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
    static class AndroidLogcatStyles
    {
        public const int kFontSize = 10;
        public const int kFixedHeight = kFontSize + 9;
        public static GUIStyle toolbar = new GUIStyle(EditorStyles.toolbar) { fontSize = kFontSize, fixedHeight = kFixedHeight};
        public static GUIStyle toolbarButton = new GUIStyle(EditorStyles.toolbarButton) { fontSize = kFontSize, fixedHeight = kFixedHeight };
        public static GUIStyle toolbarPopup = new GUIStyle(EditorStyles.toolbarPopup) { fontSize = kFontSize, fixedHeight = kFixedHeight };
        public static GUIStyle toolbarSearchField = new GUIStyle(EditorStyles.toolbarSearchField) { fontSize = kFontSize, fixedHeight = kFixedHeight };

        public static GUIStyle toolbarLabel = new GUIStyle(EditorStyles.miniLabel) { fontSize = kFontSize, padding = new RectOffset(5, 5, 0, 0) };
        public static GUIStyle button = new GUIStyle(EditorStyles.miniButton) { fontSize = kFontSize};

        public static GUIStyle columnHeader = new GUIStyle("OL TITLE");

        public const int kLogEntryFontSize = 10;
        public const int kLogEntryFixedHeight = kLogEntryFontSize + 5;
        public static GUIStyle background = new GUIStyle("CN EntryBackodd") { fixedHeight = kLogEntryFixedHeight };
        public static GUIStyle priorityDefaultStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = kLogEntryFontSize, fixedHeight = kLogEntryFixedHeight, padding = new RectOffset(0, 0, 1, 1) };
        public static GUIStyle[] priorityStyles = new[]
        {
            new GUIStyle(priorityDefaultStyle) {},
            new GUIStyle(priorityDefaultStyle) {},
            new GUIStyle(priorityDefaultStyle) {},
            new GUIStyle(priorityDefaultStyle) { normal = new GUIStyleState() { textColor = Color.yellow } },
            new GUIStyle(priorityDefaultStyle) { normal = new GUIStyleState() { textColor = Color.red } },
            new GUIStyle(priorityDefaultStyle) { normal = new GUIStyleState() { textColor = Color.red } },
        };

        public const int kStatusBarFontSize = 13;
        public const int kLStatusBarFixedHeight = kStatusBarFontSize + 5;
        public static GUIStyle statusBarBackground = new GUIStyle("AppToolbar") { fixedHeight = kStatusBarFontSize };
        public static GUIStyle statusLabel = new GUIStyle("AppToolbar") { fontSize = kStatusBarFontSize, fixedHeight = kLStatusBarFixedHeight, richText = true };
    }
}
#endif
