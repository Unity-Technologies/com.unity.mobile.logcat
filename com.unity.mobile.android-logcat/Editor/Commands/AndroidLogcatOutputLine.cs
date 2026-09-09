using UnityEngine;

namespace Unity.Android.Logcat
{
    internal readonly struct AndroidLogcatOutputLine
    {
        internal string Text { get; }

        internal Color LineColor { get; }

        internal AndroidLogcatOutputLine(string text, Color color)
        {
            Text = text;
            LineColor = color;
        }
    }
}
