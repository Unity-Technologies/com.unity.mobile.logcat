using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Zoom and pan for an image drawn into a rect handed down by a window. The live
    /// view and the saved screenshots both draw through it, so a closer look at either
    /// works the same way.
    /// <para>
    /// Ctrl and the wheel zoom between 100% and 4000%, Ctrl and a middle mouse button
    /// drag move the zoomed image, and scrollbars appear as soon as there is more image
    /// than there is room for it. Everything else - clicks, a plain wheel, keys - is
    /// left alone, because the live view forwards all of that to the device.
    /// </para>
    /// <para>
    /// View state, and not serialized: the zoom goes back to 100% on a domain reload,
    /// the same as the scroll position of any other IMGUI view.
    /// </para>
    /// </summary>
    internal class AndroidLogcatImageViewer
    {
        static class Styles
        {
            // Ctrl on Windows and Linux, Cmd on macOS - the same modifier the live
            // view's editing shortcuts use.
            static readonly string kModifier =
                Application.platform == RuntimePlatform.OSXEditor ? "Cmd" : "Ctrl";

            static readonly string Gestures =
                $"{kModifier}+Wheel over the image zooms between 100% and 4000%, " +
                $"{kModifier}+Middle mouse drag moves the zoomed image.";

            internal static GUIContent Zoom(int percent)
            {
                return new GUIContent($"{percent}%", Gestures);
            }

            static GUIStyle s_Badge;

            /// <summary>
            /// A help box that does not wrap. The standard one does, and CalcSize being
            /// a fraction of a pixel short of what it then needs is enough to break
            /// "168%" across two lines.
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

        // Four notches of the wheel double the zoom, so twenty-two of them cross the
        // whole range either way. A factor rather than a fixed step, because a step
        // that is a sensible move at 100% is an invisible one at 4000%.
        const float kWheelDeltaPerNotch = 3.0f;
        const float kNotchesPerDoubling = 4.0f;
        const int kMiddleMouseButton = 2;
        const float kBadgeMargin = 4;
        // Deliberately more than a scrollbar really takes. It only bounds how far the
        // image can be moved, and BeginScrollView clamps what it is handed anyway - so
        // guessing high costs nothing, where guessing low would leave a strip of the
        // image that cannot be reached.
        const float kScrollbarSize = 20;

        float m_Zoom = kMinZoom;
        Vector2 m_Scroll;
        bool m_Panning;

        internal float Zoom => m_Zoom;
        internal Vector2 Scroll => m_Scroll;

        /// <summary>The zoom as it is shown: 100 to 300.</summary>
        internal int ZoomPercent => Mathf.RoundToInt(m_Zoom * 100);

        /// <summary>
        /// Whether the image is larger than the area showing it, i.e. whether there is
        /// anything to scroll or pan to.
        /// </summary>
        internal bool IsZoomed => m_Zoom > kMinZoom;

        internal void Reset()
        {
            m_Zoom = kMinZoom;
            m_Scroll = Vector2.zero;
        }

        /// <summary>
        /// Draws an image of the given aspect ratio into <paramref name="area"/>, zoomed
        /// and panned as the user left it.
        /// </summary>
        /// <param name="drawContents">
        /// Handed the rect the image occupies, and draws it. A callback rather than a
        /// returned rect, because that rect only means anything between the scroll
        /// view's begin and end: the live view reads the mouse against it, and inside
        /// the scroll view mouse positions are in the same space.
        /// </param>
        /// <param name="repaint">
        /// Called when zooming or panning changed something. Without it a view that is
        /// not already repainting - a stopped stream showing its last frame, or a
        /// screenshot - would not show the zoom until something else caused a repaint.
        /// </param>
        /// <returns>
        /// The box the image is seen through, in the window's coordinates, for laying
        /// out whatever sits beside the image - it is not the whole area, and it moves
        /// with the zoom.
        /// </returns>
        internal Rect DoGUI(Rect area, float aspect, Action<Rect> drawContents, Action repaint)
        {
            // Allocated on every pass whatever the state, so that the ids handed out
            // after it do not shift between the Layout and Repaint passes.
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            // Both before the scroll view, so that neither it nor the contents get these
            // events first: the live view forwards a plain wheel to the device, and the
            // scroll view would scroll on it. They are handled against the box as it was
            // drawn, which is why it is worked out before them and again after.
            var box = ViewBox(area, aspect, out _);
            HandleZoom(area, aspect, box, repaint);
            HandlePan(controlId, area, aspect, box, repaint);

            box = ViewBox(area, aspect, out var image);

            // The content is the image itself, so there is never empty space to scroll
            // into, and the box is only as big as the image needs - so the scrollbars
            // come up against the image rather than against the far side of whatever
            // room it was given.
            var content = new Rect(0, 0, image.x, image.y);

            m_Scroll = GUI.BeginScrollView(box, m_Scroll, content);
            drawContents(content);
            GUI.EndScrollView();

            // After the scroll view, in the window's own coordinates: inside it, the
            // badge would scroll away with the image.
            if (IsZoomed)
                DoZoomBadgeGUI(box);

            return box;
        }

        /// <summary>
        /// The box the image is seen through, centred in the area, and
        /// <paramref name="image"/> - the size the image is drawn at.
        /// <para>
        /// The box is the image's own size until the image outgrows the area, and then
        /// it is the area: at 100% that is exactly the fitted image, so the view looks
        /// the same as it did before there was a zoom, and a zoomed portrait screen
        /// keeps the scrollbar and anything laid out beside it against its edge instead
        /// of stranding them across the letterbox. Room is left for whichever scrollbar
        /// the image is about to need.
        /// </para>
        /// </summary>
        Rect ViewBox(Rect area, float aspect, out Vector2 image)
        {
            var fitted = FitRect(area, aspect);
            image = new Vector2(fitted.width, fitted.height) * m_Zoom;

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

        /// <summary>
        /// The largest rect of the given aspect ratio that fits inside the container,
        /// centred in it. Fitted explicitly rather than with ScaleMode.ScaleToFit,
        /// because the fitted rect is also what mouse positions are mapped through.
        /// </summary>
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
        /// <paramref name="pointer"/> where it is: zooming towards a corner otherwise
        /// walks it off the edge, and it has to be panned back afterwards. Positive
        /// deltas zoom out, which is the direction the wheel reports for scrolling down.
        /// </summary>
        /// <returns>
        /// Whether the zoom changed, i.e. whether it was not already at the end of its
        /// range.
        /// </returns>
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

            // Where the pointer is on the image, as a fraction of it, so that the same
            // point can be put back under it once the image has changed size.
            var pointOnImage = new Vector2(
                (pointer.x - box.x + m_Scroll.x) / image.x,
                (pointer.y - box.y + m_Scroll.y) / image.y);

            m_Zoom = zoom;

            // The box moves as well as the image: it grows until it fills the area, so
            // the same point is at a different place on screen even before scrolling.
            var zoomedBox = ViewBox(area, aspect, out var zoomedImage);
            m_Scroll = new Vector2(
                pointOnImage.x * zoomedImage.x - (pointer.x - zoomedBox.x),
                pointOnImage.y * zoomedImage.y - (pointer.y - zoomedBox.y));
            ClampScroll(area, aspect);
            return true;
        }

        /// <summary>
        /// Moves the visible part of the image by a mouse movement. The image follows
        /// the mouse, so the view moves the other way.
        /// </summary>
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

            // Used whether or not the zoom moved: at either end of the range the wheel
            // is still zooming, and letting it through would scroll the view or, in the
            // live view, the device.
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
                    // The middle button, under the same modifier as the zoom, so the two
                    // are one gesture to learn. Nothing else in either view uses that
                    // button, but the modifier keeps this out of the way if something
                    // ever does.
                    if (e.button != kMiddleMouseButton || !IsViewModifier(e) || !IsZoomed)
                        break;
                    // Not while something else is being dragged - a touch being held on
                    // the device, say.
                    if (GUIUtility.hotControl != 0 || !box.Contains(e.mousePosition))
                        break;
                    // Taking the hot control is what routes the rest of the drag here,
                    // including the part that happens outside the area.
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

        /// <summary>
        /// Ctrl, or Cmd on macOS. Both are taken on both platforms: this is view
        /// navigation rather than a command, and nothing is lost by accepting either.
        /// </summary>
        static bool IsViewModifier(Event e)
        {
            return (e.modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
        }

        /// <summary>
        /// Keeps the scrolled-away part between nothing and everything the box cannot
        /// show, so the image can always be moved far enough to see its far edge and no
        /// further.
        /// </summary>
        void ClampScroll(Rect area, float aspect)
        {
            var box = ViewBox(area, aspect, out var image);

            // The scrollbars sit inside the box, so each one takes a strip off what is
            // left to see the image through.
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
            // Rounded up, because a rect a fraction narrower than the text is a rect
            // the text does not fit in.
            var size = Styles.Badge.CalcSize(content);
            var rect = new Rect(area.x + kBadgeMargin, area.y + kBadgeMargin,
                Mathf.Min(Mathf.Ceil(size.x), area.width),
                Mathf.Min(Mathf.Ceil(size.y), area.height));
            GUI.Label(rect, content, Styles.Badge);
        }
    }
}
