using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    class Splitter
    {
        internal enum SplitterType
        {
            Horizontal,
            Vertical,
        }

        private SplitterType m_SplitterType;
        private float m_MinValue;
        private float m_MaxValue;
        private bool m_Dragging;
        private float m_Start;
        private float m_OldValue;

        public bool Dragging => m_Dragging;

        internal Splitter(SplitterType splitterType, float minValue, float maxValue)
        {
            m_SplitterType = splitterType;
            m_MinValue = minValue;
            m_MaxValue = maxValue;
        }

        internal bool DoGUI(Rect splitterBorders, ref float valueToChange)
        {
            valueToChange = Mathf.Clamp(valueToChange, m_MinValue, m_MaxValue);
            switch (m_SplitterType)
            {
                case SplitterType.Horizontal:
                    EditorGUIUtility.AddCursorRect(splitterBorders, MouseCursor.ResizeHorizontal);
                    break;
                case SplitterType.Vertical:
                    EditorGUIUtility.AddCursorRect(splitterBorders, MouseCursor.ResizeVertical);
                    break;
            }

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterBorders.Contains(e.mousePosition))
                    {
                        m_Dragging = true;
                        m_OldValue = valueToChange;
                        switch (m_SplitterType)
                        {
                            case SplitterType.Horizontal: m_Start = e.mousePosition.x; break;
                            case SplitterType.Vertical: m_Start = e.mousePosition.y; break;
                        }
                        e.Use();
                        return true;
                    }

                    break;
                case EventType.MouseDrag:
                case EventType.MouseUp:
                    if (!m_Dragging)
                        return false;

                    switch (m_SplitterType)
                    {
                        case SplitterType.Vertical:
                            valueToChange = Mathf.Clamp(m_OldValue + m_Start - e.mousePosition.y, m_MinValue, m_MaxValue);
                            if (e.type == EventType.MouseUp)
                                ClearOperation();
                            e.Use();
                            return true;
                        case SplitterType.Horizontal:
                            valueToChange = Mathf.Clamp(m_OldValue + e.mousePosition.x - m_Start, m_MinValue, m_MaxValue);
                            if (e.type == EventType.MouseUp)
                                ClearOperation();
                            e.Use();
                            return true;
                    }
                    break;
            }

            return false;
        }
        private void ClearOperation()
        {
            m_Dragging = false;
            m_Start = 0.0f;
            m_OldValue = 0.0f;
        }
    }
}
