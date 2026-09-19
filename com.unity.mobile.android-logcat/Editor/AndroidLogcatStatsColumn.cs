using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// The column of name/value rows beside the image in the Screen Capture window,
    /// shared by the live view and the screenshot preview so the two look alike.
    /// </summary>
    internal static class AndroidLogcatStatsColumn
    {
        const float kWidth = 190;
        const float kMargin = 8;

        internal const float kLabelWidth = 80;

        /// <summary>
        /// Width to reserve out of an area before the image is fitted into it. Capped
        /// to a fraction of it, so a narrow window does not lose the image to the column.
        /// </summary>
        internal static float WidthFor(Rect area)
        {
            return Mathf.Min(kWidth, area.width * 0.4f);
        }

        /// <summary>
        /// The same, widened to hold the values it is given - a device name is longer
        /// than anything the live view shows - and capped the same way, so a narrow
        /// window keeps its image rather than losing it to the column.
        /// </summary>
        internal static float WidthFor(Rect area, string[] values)
        {
            var widest = 0.0f;
            foreach (var value in values)
                widest = Mathf.Max(widest, Mathf.Ceil(EditorStyles.miniLabel.CalcSize(new GUIContent(value)).x));

            return Mathf.Min(Mathf.Max(kWidth, kLabelWidth + widest + kMargin), area.width * 0.4f);
        }

        /// <summary>
        /// The column's rect, against the image rather than the right edge of the area:
        /// the image is centred in what is left over, so the gap beside it varies.
        /// </summary>
        internal static Rect RectBeside(Rect area, Rect imageBox)
        {
            return new Rect(imageBox.xMax + kMargin, imageBox.y,
                Mathf.Max(0, area.xMax - imageBox.xMax - kMargin), imageBox.height);
        }

        internal static void Row(Rect rc, float labelWidth, ref float y, GUIContent name, string value,
            string valueTooltip = null)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (y + height > rc.yMax)
                return;

            GUI.Label(new Rect(rc.x, y, labelWidth, height), name, EditorStyles.miniLabel);
            // The column is narrow enough that long values clip, so the tooltip carries
            // the full text where that matters.
            GUI.Label(new Rect(rc.x + labelWidth, y, Mathf.Max(0, rc.width - labelWidth), height),
                new GUIContent(value, valueTooltip ?? name.tooltip), EditorStyles.miniLabel);
            y += height;
        }
    }
}
