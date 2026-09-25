using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Zoom and pan for an image drawn into a rect handed down by a window - the live
    /// view and the saved screenshots both draw through it. Ctrl and the wheel zoom,
    /// Ctrl and a left or middle mouse drag move the zoomed image, and scrollbars appear with
    /// it. Everything else is left alone, because the live view forwards clicks, the
    /// plain wheel and keys to the device.
    /// </summary>
    internal class AndroidLogcatImageViewer
    {
        static class Styles
        {
            static readonly string kModifier =
                Application.platform == RuntimePlatform.OSXEditor ? "Cmd" : "Ctrl";

            static readonly string Gestures =
                $"{kModifier}+Wheel over the image zooms between 100% and 4000%, " +
                $"{kModifier}+Left or middle mouse drag moves the zoomed image";

            internal static GUIContent Zoom(int percent)
            {
                return new GUIContent($"{percent}%", Gestures);
            }

            static GUIStyle s_Badge;

            /// <summary>
            /// A help box that does not wrap. The standard one does, and CalcSize being
            /// a fraction short of what it then needs breaks "168%" across two lines.
            /// </summary>
            internal static GUIStyle Badge
            {
                get
                {
                    if (s_Badge == null)
                    {
                        s_Badge = new GUIStyle(EditorStyles.helpBox);
                        s_Badge.wordWrap = false;
                        s_Badge.alignment = TextAnchor.MiddleCenter;
                        s_Badge.padding = new RectOffset(6, 6, 2, 2);
                    }
                    return s_Badge;
                }
            }
        }

        internal const float kMinZoom = 1.0f;
        internal const float kMaxZoom = 40.0f;

        // Four notches of the wheel double the zoom. A factor rather than a fixed step,
        // because a step that is a sensible move at 100% is invisible at 4000%.
        const float kWheelDeltaPerNotch = 3.0f;
        const float kNotchesPerDoubling = 4.0f;
        const int kLeftMouseButton = 0;
        const int kMiddleMouseButton = 2;
        const float kBadgeMargin = 4;
        // Deliberately more than a scrollbar takes. It only bounds how far the image can
        // be moved, and BeginScrollView clamps what it is handed, so guessing high costs
        // nothing where guessing low leaves a strip of the image unreachable.
        const float kScrollbarSize = 20;

        float m_Zoom = kMinZoom;
        Vector2 m_Scroll;
        bool m_Panning;

        internal float Zoom => m_Zoom;
        internal Vector2 Scroll => m_Scroll;
        internal int ZoomPercent => Mathf.RoundToInt(m_Zoom * 100);
        internal bool IsZoomed => m_Zoom > kMinZoom;

        internal void Reset()
        {
            m_Zoom = kMinZoom;
            m_Scroll = Vector2.zero;
        }

        /// <summary>
        /// Draws an image of the given aspect ratio into <paramref name="area"/> and
        /// returns the box it is seen through, for laying out whatever sits beside it.
        /// <paramref name="drawContents"/> is handed the image rect, which is only
        /// meaningful inside the scroll view - the same space the live view reads the
        /// mouse in. <paramref name="repaint"/> covers the views that are not already
        /// repainting, a stopped stream or a screenshot.
        /// </summary>
        internal Rect DoGUI(Rect area, float aspect, Action<Rect> drawContents, Action repaint)
        {
            // Allocated on every pass whatever the state, so that the ids handed out
            // after it do not shift between the Layout and Repaint passes.
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            // Both before the scroll view, so neither it nor the contents see these
            // events first: the live view forwards a plain wheel to the device, and the
            // scroll view would scroll on it.
            var box = ViewBox(area, aspect, out _);
            HandleZoom(area, aspect, box, repaint);
            HandlePan(controlId, area, aspect, box, repaint);

            box = ViewBox(area, aspect, out var image);
            var content = new Rect(0, 0, image.x, image.y);

            m_Scroll = GUI.BeginScrollView(box, m_Scroll, content);
            drawContents(content);
            GUI.EndScrollView();

            // Outside the scroll view, or it would scroll away with the image.
            if (IsZoomed)
                DoZoomBadgeGUI(box);

            return box;
        }

        /// <summary>
        /// The box the image is seen through, centred in the area, and the size the
        /// image is drawn at. The box is the image's own size until the image outgrows
        /// the area: at 100% that is exactly the fitted image, and a zoomed portrait
        /// screen keeps the scrollbar against its edge rather than across the letterbox.
        /// </summary>
        Rect ViewBox(Rect area, float aspect, out Vector2 image)
        {
            var fitted = FitRect(area, aspect);
            image = new Vector2(fitted.width, fitted.height) * m_Zoom;

            // Room for whichever scrollbar the image is about to need.
            var want = image;
            if (image.y > area.height)
                want.x += kScrollbarSize;
            if (image.x > area.width)
                want.y += kScrollbarSize;

            var size = new Vector2(Mathf.Min(area.width, want.x), Mathf.Min(area.height, want.y));
            return new Rect(
                area.x + (area.width - size.x) * 0.5f,
                area.y + (area.height - size.y) * 0.5f,
                size.x, size.y);
        }

        static Rect FitRect(Rect container, float aspect)
        {
            if (container.width <= 0 || container.height <= 0 || aspect <= 0)
                return container;

            if (aspect > container.width / container.height)
            {
                var height = container.width / aspect;
                return new Rect(container.x, container.y + (container.height - height) * 0.5f, container.width, height);
            }

            var width = container.height * aspect;
            return new Rect(container.x + (container.width - width) * 0.5f, container.y, width, container.height);
        }

        /// <summary>
        /// Zooms by one wheel movement, keeping whatever is under
        /// <paramref name="pointer"/> where it is. Positive deltas zoom out, matching
        /// the wheel. Returns false when the zoom was already at the end of its range.
        /// </summary>
        internal bool ZoomAt(Rect area, float aspect, Vector2 pointer, float wheelDelta)
        {
            if (area.width <= 0 || area.height <= 0)
                return false;

            var doublings = -wheelDelta / (kWheelDeltaPerNotch * kNotchesPerDoubling);
            var zoom = Mathf.Clamp(m_Zoom * Mathf.Pow(2.0f, doublings), kMinZoom, kMaxZoom);
            if (Mathf.Approximately(zoom, m_Zoom))
                return false;

            var box = ViewBox(area, aspect, out var image);
            if (image.x <= 0 || image.y <= 0)
            {
                m_Zoom = zoom;
                return true;
            }

            var pointOnImage = new Vector2(
                (pointer.x - box.x + m_Scroll.x) / image.x,
                (pointer.y - box.y + m_Scroll.y) / image.y);

            m_Zoom = zoom;

            // The box moves as well as the image, growing until it fills the area, so
            // the same point is somewhere else on screen even before scrolling.
            var zoomedBox = ViewBox(area, aspect, out var zoomedImage);
            m_Scroll = new Vector2(
                pointOnImage.x * zoomedImage.x - (pointer.x - zoomedBox.x),
                pointOnImage.y * zoomedImage.y - (pointer.y - zoomedBox.y));
            ClampScroll(area, aspect);
            return true;
        }

        /// <summary>Moves the image with the mouse, so the view moves the other way.</summary>
        internal void Pan(Rect area, float aspect, Vector2 mouseDelta)
        {
            m_Scroll -= mouseDelta;
            ClampScroll(area, aspect);
        }

        void HandleZoom(Rect area, float aspect, Rect box, Action repaint)
        {
            var e = Event.current;
            if (e.type != EventType.ScrollWheel || !IsViewModifier(e))
                return;
            if (!box.Contains(e.mousePosition))
                return;

            // Used at either end of the range too: the wheel is still zooming, and
            // letting it through would scroll the view or, in the live view, the device.
            e.Use();

            if (ZoomAt(area, aspect, e.mousePosition, e.delta.y))
                repaint?.Invoke();
        }

        void HandlePan(int controlId, Rect area, float aspect, Rect box, Action repaint)
        {
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    // The left button as well as the middle one: a trackpad has no
                    // middle button, so on macOS there would be no way to pan at all.
                    // The modifier is what keeps a plain drag going to the device.
                    if ((e.button != kMiddleMouseButton && e.button != kLeftMouseButton)
                        || !IsViewModifier(e) || !IsZoomed)
                        break;
                    // Not while something else is being dragged - a touch being held on
                    // the device, say.
                    if (GUIUtility.hotControl != 0 || !box.Contains(e.mousePosition))
                        break;
                    // Routes the rest of the drag here, including outside the box.
                    GUIUtility.hotControl = controlId;
                    m_Panning = true;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!m_Panning)
                        break;
                    // The modifier is deliberately not rechecked: letting go of Ctrl
                    // halfway through a drag should not abandon it.
                    Pan(area, aspect, e.delta);
                    e.Use();
                    repaint?.Invoke();
                    break;

                case EventType.MouseUp:
                    if (!m_Panning)
                        break;
                    EndPan(controlId);
                    e.Use();
                    break;
            }

            // A drag that left the window never reports its button going up.
            if (m_Panning && e.type == EventType.MouseLeaveWindow)
                EndPan(controlId);
        }

        void EndPan(int controlId)
        {
            m_Panning = false;
            if (GUIUtility.hotControl == controlId)
                GUIUtility.hotControl = 0;
        }

        static bool IsViewModifier(Event e)
        {
            return (e.modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
        }

        void ClampScroll(Rect area, float aspect)
        {
            var box = ViewBox(area, aspect, out var image);

            // The scrollbars sit inside the box, so each takes a strip off what is left.
            var visible = new Vector2(
                box.width - (image.y > box.height ? kScrollbarSize : 0),
                box.height - (image.x > box.width ? kScrollbarSize : 0));

            m_Scroll = new Vector2(
                Mathf.Clamp(m_Scroll.x, 0, Mathf.Max(0, image.x - visible.x)),
                Mathf.Clamp(m_Scroll.y, 0, Mathf.Max(0, image.y - visible.y)));
        }

        void DoZoomBadgeGUI(Rect area)
        {
            var content = Styles.Zoom(ZoomPercent);
            var size = Styles.Badge.CalcSize(content);
            var rect = new Rect(area.x + kBadgeMargin, area.y + kBadgeMargin,
                Mathf.Min(Mathf.Ceil(size.x), area.width),
                Mathf.Min(Mathf.Ceil(size.y), area.height));
            GUI.Label(rect, content, Styles.Badge);
        }
    }
}
