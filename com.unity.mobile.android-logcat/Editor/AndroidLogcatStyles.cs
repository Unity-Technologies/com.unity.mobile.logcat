using UnityEngine;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// UI styles.
    /// Note: Don't use EditorStyles, since they're not initialized if Editor is ran in batch mode, for ex., tests
    /// </summary>
    [NoAutoStaticsCleanup]
    static class AndroidLogcatStyles
    {
        public const int kFontSize = 10;
        public const int kFixedHeight = kFontSize + 9;
        public static readonly GUIStyle toolbar = new GUIStyle("toolbar") { fontSize = kFontSize, fixedHeight = kFixedHeight };
        public static readonly GUIStyle toolbarButton = new GUIStyle("toolbarButton") { fontSize = kFontSize, fixedHeight = kFixedHeight };
        public static readonly GUIStyle toolbarPopup = new GUIStyle("toolbarPopup") { fontSize = kFontSize, fixedHeight = kFixedHeight };
        public static readonly GUIStyle toolbarPopupCenter = new GUIStyle("toolbarPopup") { fontSize = kFontSize, fixedHeight = kFixedHeight, alignment = TextAnchor.MiddleCenter };
        public static readonly GUIStyle toolbarLabelLeft = new GUIStyle("toolbarButton") { fontSize = kFontSize, fixedHeight = kFixedHeight, alignment = TextAnchor.MiddleLeft };

        public static readonly GUIStyle columnHeader = new GUIStyle("OL TITLE");

        public static int kLogEntryFontSize = 11;
        public static int kLogEntryFixedHeight = kLogEntryFontSize + 5;
        public static readonly GUIStyle background = new GUIStyle("CN EntryBackodd") { fixedHeight = kLogEntryFixedHeight };
        public static readonly GUIStyle backgroundOdd = new GUIStyle("CN EntryBackodd") { fixedHeight = kLogEntryFixedHeight };
        public static readonly GUIStyle backgroundEven = new GUIStyle("CN EntryBackEven") { fixedHeight = kLogEntryFixedHeight };

        public static readonly Vector2 kSmallIconSize = new Vector2(16, 16);
        public static readonly GUIStyle infoSmallStyle = new GUIStyle("CN EntryInfoIconSmall") { fixedHeight = kLogEntryFixedHeight };
        public static readonly GUIStyle warningSmallStyle = new GUIStyle("CN EntryWarnIconSmall") { fixedHeight = kLogEntryFixedHeight };
        public static readonly GUIStyle errorSmallStyle = new GUIStyle("CN EntryErrorIconSmall") { fixedHeight = kLogEntryFixedHeight };
        public static readonly GUIStyle priorityDefaultStyle = new GUIStyle("miniLabel")
        {
            fontSize = kLogEntryFontSize,
            fixedHeight = kLogEntryFixedHeight,
            padding = new RectOffset(0, 0, 1, 1),
            font = GetFont()
        };

        public static readonly GUIStyle[] priorityStyles = new[]
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
        public static readonly GUIStyle statusBarBackground = new GUIStyle("AppToolbar") { fixedHeight = kStatusBarFontSize };
        public static readonly GUIStyle statusLabel = new GUIStyle("AppToolbar") { fontSize = kStatusBarFontSize, fixedHeight = kLStatusBarFixedHeight, richText = true };

        public const int kTagEntryFontSize = 11;
        public const int kTagEntryFixedHeight = kTagEntryFontSize + 7;
        public const int ktagToggleFixedWidth = 10;
        public static readonly GUIStyle tagEntryBackground = new GUIStyle("CN EntryBackodd") { fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle tagEntryBackgroundOdd = new GUIStyle("CN EntryBackodd") { fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle tagEntryBackgroundEven = new GUIStyle("CN EntryBackEven") { fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle tagEntryStyle = new GUIStyle("miniLabel") { fontSize = kTagEntryFontSize, fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle tagToggleStyle = new GUIStyle("toggle") { fixedWidth = ktagToggleFixedWidth, fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle tagButtonStyle = new GUIStyle("miniButton") { fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle removeTextStyle = new GUIStyle("miniLabel") { fontSize = 8, fixedHeight = kTagEntryFixedHeight };
        public static readonly GUIStyle kSeriesLabel = "ProfilerPaneSubLabel";

        public static readonly GUIStyle stacktraceStyle = new GUIStyle("textArea")
        {
            fontSize = kLogEntryFontSize,
            font = GetFont(),
            richText = true,
            wordWrap = false
        };

        public static readonly GUIStyle resolvedStacktraceStyle = new GUIStyle("textArea")
        {
            fontSize = kLogEntryFontSize,
            font = GetFont(),
            fontStyle = FontStyle.BoldAndItalic,
            richText = true,
            wordWrap = false,
        };

        public static readonly GUIStyle infoStyle = new GUIStyle("label")
        {
            fontSize = kLogEntryFontSize,
            font = GetFont()
        };

        public static readonly GUIStyle errorStyle = new GUIStyle("label")
        {
            fontSize = kLogEntryFontSize,
            font = GetFont(),
            normal = new GUIStyleState() { textColor = Color.red }
        };

        public static readonly GUIStyle internalLogStyle = new GUIStyle("CN EntryBackodd")
        {
            fixedHeight = 13,
            fontSize = 13,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            font = GetFont()
        };


        public static Font GetFont()
        {
            return (Font)EditorGUIUtility.LoadRequired("Packages/com.unity.mobile.android-logcat/Editor/Fonts/consola.ttf");
        }

        internal class StatusWheel
        {
            GUIContent[] m_StatusWheel = new GUIContent[12];
            public StatusWheel()
            {
                for (int i = 0; i < m_StatusWheel.Length; i++)
                    m_StatusWheel[i] = EditorGUIUtility.IconContent("WaitSpin" + i.ToString("00"));
            }

            public GUIContent GetContent(int index)
            {
                return m_StatusWheel[index];
            }

            public int GetMaxIndex()
            {
                return m_StatusWheel.Length - 1;
            }
        }

        private static StatusWheel m_StatusWheel;
        internal static StatusWheel Status
        {
            get
            {
                if (m_StatusWheel == null)
                    m_StatusWheel = new StatusWheel();

                return m_StatusWheel;
            }
        }

        public static readonly GUIStyle StatusIcon = "toolbarButton";

        public static Texture2D LoadIcon(string name)
        {
            var tex = (Texture2D)EditorGUIUtility.Load($"Packages/com.unity.mobile.android-logcat/Editor/Textures/{name}.png");
            return tex;
        }

        internal static readonly GUIContent kIconToolbarDown = new GUIContent(LoadIcon("DownArrow"));
        internal static readonly GUIContent kIconToolbarUp = new GUIContent(LoadIcon("UpArrow"));
        internal static readonly GUIContent kIconReset = new GUIContent(LoadIcon("Reset"), "Reset");
        internal static readonly GUIContent[] kIconPlay = new[] {
            new GUIContent(LoadIcon("PlayOn")),
            new GUIContent(LoadIcon("PlayOff"))
        };
    }
}
