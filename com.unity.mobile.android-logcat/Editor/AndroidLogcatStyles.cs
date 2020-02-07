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

        public static GUIStyle columnHeader = new GUIStyle("OL TITLE");

        public static int kLogEntryFontSize = 11;
        public static int kLogEntryFixedHeight = kLogEntryFontSize + 5;
        public static GUIStyle background = new GUIStyle("CN EntryBackodd") { fixedHeight = kLogEntryFixedHeight };
        public static GUIStyle backgroundOdd = new GUIStyle("CN EntryBackodd") { fixedHeight = kLogEntryFixedHeight };
        public static GUIStyle backgroundEven = new GUIStyle("CN EntryBackEven") { fixedHeight = kLogEntryFixedHeight };

        public static readonly Vector2 kSmallIconSize = new Vector2(16, 16);
        public static GUIStyle infoSmallStyle = new GUIStyle("CN EntryInfoIconSmall") { fixedHeight = kLogEntryFixedHeight };
        public static GUIStyle warningSmallStyle = new GUIStyle("CN EntryWarnIconSmall") { fixedHeight = kLogEntryFixedHeight };
        public static GUIStyle errorSmallStyle = new GUIStyle("CN EntryErrorIconSmall") { fixedHeight = kLogEntryFixedHeight };
        public static GUIStyle priorityDefaultStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = kLogEntryFontSize,
            fixedHeight = kLogEntryFixedHeight,
            padding = new RectOffset(0, 0, 1, 1),
            font = GetFont()
        };

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

        public const int kTagEntryFontSize = 11;
        public const int kTagEntryFixedHeight = kTagEntryFontSize + 7;
        public const int ktagToggleFixedWidth = 10;
        public static GUIStyle tagEntryBackground = new GUIStyle("CN EntryBackodd") { fixedHeight = kTagEntryFixedHeight };
        public static GUIStyle tagEntryBackgroundOdd = new GUIStyle("CN EntryBackodd") { fixedHeight = kTagEntryFixedHeight };
        public static GUIStyle tagEntryBackgroundEven = new GUIStyle("CN EntryBackEven") { fixedHeight = kTagEntryFixedHeight };
        public static GUIStyle tagEntryStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = kTagEntryFontSize, fixedHeight = kTagEntryFixedHeight };
        public static GUIStyle tagToggleStyle = new GUIStyle(EditorStyles.toggle) { fixedWidth = ktagToggleFixedWidth, fixedHeight = kTagEntryFixedHeight };
        public static GUIStyle tagButtonStyle = new GUIStyle(EditorStyles.miniButton) { fixedHeight = kTagEntryFixedHeight };
        public static GUIStyle removeTextStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 8, fixedHeight = kTagEntryFixedHeight };

        public static GUIStyle stacktraceStyle = new GUIStyle("textArea")
        {
            fontSize = kLogEntryFontSize,
            font = GetFont(),
            richText = true,
            wordWrap = false
        };

        public static GUIStyle errorStyle = new GUIStyle("label")
        {
            fontSize = kLogEntryFontSize,
            font = GetFont(),
            normal = new GUIStyleState() { textColor = Color.red }
        };


        public static Font GetFont()
        {
            return (Font)EditorGUIUtility.LoadRequired(UnityEditor.Experimental.EditorResources.fontsPath + "consola.ttf");
        }
    }
}
#endif
