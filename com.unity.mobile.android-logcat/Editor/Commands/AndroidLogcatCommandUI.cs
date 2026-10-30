using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatCommandUI
    {
        internal static readonly Color kCommandColor = new Color(0.6f, 0.6f, 0.6f);
        internal static readonly Color kRowSeparatorColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        internal static readonly Color kSelectedRowColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);
        internal static readonly Color kHintColor = new Color(0.55f, 0.55f, 0.55f);

        internal const int kRowHeight = 24;
        internal const int kButtonHeight = 18;
        internal const int kButtonSpacing = 2;

        internal static VisualElement CreateRow(int height = kRowHeight)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = kRowSeparatorColor;
            if (height > 0)
                row.style.height = height;
            return row;
        }

        internal static Label CreateNameLabel(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexBasis = new StyleLength(160f);
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
            label.style.minWidth = 60;
            Elide(label);
            return label;
        }

        internal static Label CreateCommandLabel(string text)
        {
            var label = new Label(text);
            label.style.color = new StyleColor(kCommandColor);
            label.style.flexBasis = new StyleLength(0f);
            label.style.flexGrow = 2;
            label.style.flexShrink = 1;
            label.style.minWidth = 0;
            Elide(label);
            return label;
        }

        static void Elide(Label label)
        {
            label.style.overflow = Overflow.Hidden;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
        }

        internal static Button AddRowButton(VisualElement row, string text, int width, Action action, bool enabled = true, string tooltip = null)
        {
            var btn = new Button(action) { text = text };
            btn.style.width = width;
            btn.style.minWidth = width;
            btn.style.flexShrink = 0;
            btn.style.height = kButtonHeight;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.marginLeft = kButtonSpacing;
            btn.style.marginRight = 0;
            btn.SetEnabled(enabled);
            if (!string.IsNullOrEmpty(tooltip))
            {
                if (enabled)
                    SetPointerTooltip(btn, tooltip);
                else
                    btn.tooltip = tooltip;
            }
            row.Add(btn);
            return btn;
        }

        internal static void AddSpacer(VisualElement row, int width)
        {
            var spacer = new VisualElement();
            spacer.style.width = width;
            spacer.style.minWidth = width;
            spacer.style.marginLeft = kButtonSpacing;
            spacer.style.flexShrink = 0;
            row.Add(spacer);
        }

        internal static void SetPointerTooltip(VisualElement element, string tooltip)
        {
            if (element == null || string.IsNullOrEmpty(tooltip))
                return;

            var pointerPosition = Vector2.zero;
            var hasPointerPosition = false;

            void Track(Vector2 position)
            {
                pointerPosition = position;
                hasPointerPosition = true;
            }

            element.RegisterCallback<PointerMoveEvent>(evt => Track(evt.position));
            element.RegisterCallback<PointerEnterEvent>(evt => Track(evt.position));

            element.RegisterCallback<TooltipEvent>(evt =>
            {
                evt.tooltip = tooltip;
                evt.rect = hasPointerPosition
                    ? new Rect(pointerPosition.x, pointerPosition.y, 1f, 18f)
                    : element.worldBound;
                evt.StopImmediatePropagation();
            });

            element.tooltip = tooltip;
        }
    }
}
