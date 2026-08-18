using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// A single line of command output together with the colour it should be rendered in.
    /// </summary>
    internal readonly struct AndroidLogcatOutputLine
    {
        internal string Text { get; }

        // Named LineColor rather than Color so it doesn't shadow UnityEngine.Color inside this type.
        internal Color LineColor { get; }

        internal AndroidLogcatOutputLine(string text, Color color)
        {
            Text = text;
            LineColor = color;
        }
    }
}
