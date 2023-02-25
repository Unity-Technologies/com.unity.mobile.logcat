using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Class for sending input to android device or running application
    /// </summary>
    internal class AndroidLogcatInputs
    {
        const float kMinWindowHeight = 155.0f;
        const float kMaxWindowHeight = 200.0f;

        Splitter m_VerticalSplitter;

        internal AndroidLogcatInputs()
        {
            m_VerticalSplitter = new Splitter(Splitter.SplitterType.Vertical, kMinWindowHeight, kMaxWindowHeight);
        }

        internal void DoGUI(ExtraWindowState extraWindowState)
        {
            var splitterRectVertical = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(5));

            m_VerticalSplitter.DoGUI(splitterRectVertical, ref extraWindowState.Height);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            GUILayout.BeginVertical(GUILayout.Height(extraWindowState.Height));
            GUILayout.Label("Hello");
            GUILayout.EndVertical();
            var contentsRect = GUILayoutUtility.GetLastRect();
            GUILayout.Space(4);
            GUILayout.EndHorizontal();

            GUI.Box(contentsRect, GUIContent.none, EditorStyles.helpBox);
        }
    }
}
