using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Video;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Plays a recording in the capture preview: a VideoPlayer on a hidden GameObject,
    /// rendering into a texture the caller draws, plus the controls under it - play and
    /// pause, a frame step each way and a seek slider. Recordings have a constant frame
    /// rate, so VideoPlayer's frame numbers are the file's own.
    /// </summary>
    internal class AndroidLogcatVideoPlayer : IDisposable
    {
        [NoAutoStaticsCleanup]
        static class Styles
        {
            // The icons the video clip inspector's preview toggles between, so playing
            // a recording looks the same as playing a clip.
            internal static readonly GUIContent Play = IconOrText("preAudioPlayOff", "Play", "Play the recording.");
            internal static readonly GUIContent Pause = IconOrText("preAudioPlayOn", "Pause", "Pause the recording.");

            internal static readonly GUIStyle FrameCounter = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

            static GUIContent IconOrText(string icon, string text, string tooltip)
            {
                var content = EditorGUIUtility.IconContent(icon);
                return content != null && content.image != null
                    ? new GUIContent(content.image, tooltip)
                    : new GUIContent(text, tooltip);
            }

            // The Animation window's previous and next frame buttons.
            internal static readonly GUIContent StepBack = IconOrText("Animation.PrevKey", "<", "Go back one frame.");
            internal static readonly GUIContent StepForward = IconOrText("Animation.NextKey", ">", "Go forward one frame.");

            internal static readonly GUIContent CopyDebugInfo = new GUIContent("Copy State",
                "Copy the state, the player's own view and every state change to the clipboard.");
            internal static readonly GUIContent ClearHistory = new GUIContent("Clear Events",
                "Clear the state changes, so the next ones start from an empty list.");
        }

        // Square-ish, sized to the icon rather than stretched across the column.
        const float kPlayButtonWidth = 32;
        const float kSeekGap = 2;
        // Between the play toggle and the step pair, which are drawn joined.
        const float kButtonGap = 4;
        const float kSeekPadding = 2;
        // Either side of the time, inside its box. CalcSize runs a fraction short, and
        // the box's border takes a pixel too.
        const float kTimePadding = 6;
        const float kDebugGap = 5;
        // Several seeks' worth, scrolled through rather than all on screen.
        const int kHistoryLength = 100;
        const float kScrollbarWidth = 16;
        // The history is only worth drawing if a few rows of it fit.
        const int kMinHistoryRows = 3;
        // A backward seek resent this many times before giving up on it.
        const int kMaxSeekRetries = 3;
        // A player also reads as stopped for a moment between preparing and playing.
        const double kStoppedSeconds = 0.5;

        /// <summary>
        /// What the player is doing, as one value rather than a set of flags that could
        /// contradict each other. The player decodes nothing while paused, so showing a
        /// frame without playing means playing until it is in and pausing there - which
        /// is what the in-between states are.
        /// </summary>
        internal enum State
        {
            /// <summary>No video open.</summary>
            Closed,
            Playing,
            /// <summary>Playing, with a seek on its way.</summary>
            Seeking,
            /// <summary>Paused when the seek was asked for: plays until a frame reaches the target, then pauses on it.</summary>
            SeekingToPause,
            /// <summary>Just opened: playing only until the first frame is in, then pausing on it.</summary>
            PausingOnFrame,
            Paused,
            /// <summary>The player reported an error - see <see cref="Error"/>.</summary>
            Failed
        }

        struct Transition
        {
            internal DateTime Time;
            internal State From;
            internal State To;
            internal string Reason;
        }

        private VideoPlayer m_Player;
        private GameObject m_PlayerGO;

        private State m_State = State.Closed;
        private readonly Queue<Transition> m_History = new Queue<Transition>();

        // Where a paused video stopped, for Play to start from. Paused from inside the
        // player's own callbacks, its clock carries on regardless, and resuming would
        // jump ahead by however long it sat paused. -1 when there is nothing to undo.
        long m_ResumeFrom = -1;

        // The frame a seek is on its way to. The player's own frame is the old one
        // until the seek lands, and stepping from that would undo a quick second
        // click. -1 outside a seek.
        // Kept while playing too, as a pause before the seek lands waits for it.
        long m_SeekTarget = -1;

        // The frame shown when a paused seek began. Frames the player decodes before
        // it has actually moved come from around here, and on a seek backwards they are
        // past the target without being anywhere near it. -1 outside a seek.
        long m_SeekFrom = -1;
        int m_SeekRetries;

        // Set once the open video is prepared, which isPrepared may not stay after it stops.
        bool m_Prepared;
        double m_StoppedSince = -1;

        // The slider being dragged, as its hot control, and whether the video was
        // playing when the drag began. Playing on underneath a drag would fight every
        // seek it sends, so it pauses until the drag is over.
        int m_DragControl;
        bool m_ResumeAfterDrag;

        string m_Path;
        DateTime m_PathWriteTime;

        // Where Open was asked to pause, sought once the first frame is in. -1 for none.
        long m_OpenAt = -1;

        Vector2 m_HistoryScroll;

        internal const string kDefaultName = "LogcatVideoPlayer";

        /// <param name="name">
        /// The hidden GameObject the VideoPlayer lives on. One left behind by an owner
        /// that never got to Dispose is picked up again rather than leaked, so each
        /// owner needs a name of its own - Dispose destroys whatever it found.
        /// </param>
        internal AndroidLogcatVideoPlayer(string name = kDefaultName)
        {
            m_PlayerGO = GameObject.Find(name);
            if (m_PlayerGO == null)
            {
                m_PlayerGO = new GameObject(name)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            m_Player = m_PlayerGO.GetComponent<VideoPlayer>();
            if (m_Player == null)
                m_Player = m_PlayerGO.AddComponent<VideoPlayer>();
            m_Player.renderMode = VideoRenderMode.APIOnly;
            m_Player.isLooping = true;
            m_Player.errorReceived += ErrorReceived;
            m_Player.seekCompleted += SeekCompleted;
            m_Player.frameReady += FrameReady;
            m_Player.loopPointReached += LoopPointReached;
            m_Player.prepareCompleted += PrepareCompleted;
        }

        internal State CurrentState => m_State;

        /// <summary>
        /// The one place the state changes. Frame events cost a callback per decoded
        /// frame, so they are on only while a state is waiting for one.
        /// </summary>
        private void SetState(State state, string reason)
        {
            if (state == m_State)
                return;

            Record(state, reason);
            m_State = state;
            if (state != State.Paused)
                m_ResumeFrom = -1;
            if (state != State.Seeking && state != State.SeekingToPause && state != State.PausingOnFrame)
            {
                m_SeekTarget = -1;
                m_SeekFrom = -1;
            }
            var waiting = state == State.SeekingToPause || state == State.PausingOnFrame;
            EditorApplication.update -= CheckStopped;
            m_StoppedSince = -1;
            if (waiting && m_Player != null)
                EditorApplication.update += CheckStopped;
            if (m_Player != null)
            {
                m_Player.sendFrameReadyEvents = waiting;
                // Only playback loops. Waiting for a frame past the end would otherwise
                // wrap around and play the whole video again to reach it.
                m_Player.isLooping = state == State.Playing || state == State.Seeking;
            }
        }

        /// <summary>
        /// Something worth seeing in the history that does not change the state - what
        /// the player reported while a state waited on it.
        /// </summary>
        private void Note(string reason)
        {
            Record(m_State, reason);
        }

        private void Record(State to, string reason)
        {
            m_History.Enqueue(new Transition { Time = DateTime.Now, From = m_State, To = to, Reason = reason });
            while (m_History.Count > kHistoryLength)
                m_History.Dequeue();
        }

        private void PrepareCompleted(VideoPlayer source)
        {
            m_Prepared = true;
            Note($"prepared, {source.frameCount} frames at {source.frameRate:0.###} fps");
        }

        /// <summary>
        /// macOS stops a one frame video without a frame or loop point event, which
        /// would leave a waiting state waiting forever.
        /// </summary>
        private void CheckStopped()
        {
            if (m_Player == null || !m_Prepared || m_Player.isPlaying || m_Player.isPaused)
            {
                m_StoppedSince = -1;
                return;
            }
            var now = EditorApplication.timeSinceStartup;
            if (m_StoppedSince < 0)
                m_StoppedSince = now;
            if (now - m_StoppedSince < kStoppedSeconds)
                return;

            m_StoppedSince = -1;
            if (m_Player.texture == null)
            {
                Error = "The player stopped without decoding a frame.";
                SetState(State.Failed, "stopped without a frame");
                return;
            }
            var last = FrameCount - 1;
            SetState(State.Paused, $"stopped, on {last}");
            m_ResumeFrom = last;
        }

        private void ErrorReceived(VideoPlayer source, string message)
        {
            // Stop video manually, otherwise it will spam Editor console.
            AndroidLogcatInternalLog.Log($"Error received while playing video, stopping video.\n{message}");
            Error = message;
            source.Stop();
            SetState(State.Failed, "error");
        }

        private void SeekCompleted(VideoPlayer source)
        {
            switch (m_State)
            {
                case State.Seeking:
                    SetState(State.Playing, "seek landed");
                    break;
                // Done is not always done: the time can still be the old one, and then
                // FrameReady decides. When it already reads as the target, the frame is
                // in, and no frame event may follow for it.
                case State.SeekingToPause:
                    var shown = Frame;
                    Note($"seek reported done, time {source.time:0.000}s, shown {shown}");
                    if (m_SeekTarget >= 0 && shown == m_SeekTarget)
                    {
                        source.Pause();
                        SetState(State.Paused, $"seek landed on {shown}");
                        m_ResumeFrom = shown;
                    }
                    break;
            }
        }

        /// <summary>
        /// Only reached while waiting, when nothing loops: what was being waited for is
        /// not coming, and the last frame is the nearest there is.
        /// </summary>
        private void LoopPointReached(VideoPlayer source)
        {
            if (m_State != State.SeekingToPause && m_State != State.PausingOnFrame)
                return;
            var last = FrameCount - 1;
            source.Pause();
            SetState(State.Paused, $"reached the end, on {last}");
            m_ResumeFrom = last;
        }

        /// <summary>
        /// Only sent while waiting for a frame. A paused seek pauses on the first frame
        /// at or past its target - neither "seek done" nor "the next frame after it" is
        /// that frame.
        /// </summary>
        private void FrameReady(VideoPlayer source, long frameIdx)
        {
            var shown = frameIdx;
            switch (m_State)
            {
                case State.PausingOnFrame:
                    source.Pause();
                    SetState(State.Paused, $"frame {shown} in");
                    m_ResumeFrom = shown;
                    // A seek needs the video open, which the first frame says it is.
                    if (m_OpenAt >= 0)
                    {
                        var openAt = m_OpenAt;
                        m_OpenAt = -1;
                        SeekTo(openAt);
                    }
                    break;
                case State.SeekingToPause:
                    Note($"frame ready: {frameIdx}, time {source.time:0.000}s");
                    var target = m_SeekTarget;
                    if (target >= 0 && shown < target)
                        break;
                    // Past the target on the way back is still where the seek started from.
                    if (target >= 0 && shown > target && target < m_SeekFrom && shown >= m_SeekFrom)
                    {
                        // Moving on from there means the seek was dropped, as macOS does
                        // with one sent straight after Play.
                        if (shown > m_SeekFrom + 1 && m_SeekRetries < kMaxSeekRetries)
                        {
                            m_SeekRetries++;
                            m_SeekFrom = shown;
                            Note($"seek to {target} dropped, sending it again");
                            MoveTo(target);
                        }
                        break;
                    }
                    source.Pause();
                    SetState(State.Paused, shown == target ? $"on {shown}" : $"on {shown}, wanted {target}");
                    m_ResumeFrom = shown;
                    break;
            }
        }

        public void Dispose()
        {
            if (m_Player != null)
            {
                m_Player.Stop();
                m_Player = null;
            }

            if (m_PlayerGO != null)
            {
                GameObject.DestroyImmediate(m_PlayerGO);
                m_PlayerGO = null;
            }
            SetState(State.Closed, "disposed");
        }

        /// <summary>Why the current video could not be played, empty while it can.</summary>
        internal string Error { get; private set; } = string.Empty;

        /// <summary>
        /// Opens a video paused on <paramref name="frame"/>, or closes the current one when
        /// <paramref name="path"/> is empty.
        /// </summary>
        public void Open(string path, long frame = 0)
        {
            Error = string.Empty;
            m_OpenAt = frame > 0 ? frame : -1;
            m_Path = path;
            m_PathWriteTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;

            if (m_Player == null)
            {
                AndroidLogcatInternalLog.Log($"Cannot open '{path}', video player was not created ?");
                SetState(State.Closed, "no player");
                return;
            }

            m_Player.Stop();
            m_DragControl = 0;
            m_ResumeAfterDrag = false;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // Releases the file, which Windows cannot rename or delete while open.
                m_Player.url = string.Empty;
                SetState(State.Closed, "closed");
                return;
            }

            m_Prepared = false;
            m_Player.url = path;
            // There is no seek to wait for, only the first frame.
            SetState(State.PausingOnFrame, $"opened {Path.GetFileName(path)}");
            m_SeekTarget = -1;
            m_Player.Play();
        }

        /// <summary>
        /// Whether playback is what the user asked for. A paused video that is playing
        /// only to show a frame is still paused as far as anyone looking at it can tell.
        /// </summary>
        public bool IsPlaying()
        {
            return m_State == State.Playing || m_State == State.Seeking;
        }

        /// <summary>
        /// Whether the image can change without anything else happening, so whoever
        /// shows it has to keep repainting. A paused video draws nothing new on its own,
        /// but one on its way to a frame does.
        /// </summary>
        internal bool NeedsRepaint => m_State == State.Playing || m_State == State.Seeking
            || m_State == State.SeekingToPause || m_State == State.PausingOnFrame;

        /// <summary>
        /// The frame to draw, or null until the player has one. A video is opened
        /// asynchronously, so there is nothing to show for the first few frames.
        /// </summary>
        internal Texture Texture => m_Player != null ? m_Player.texture : null;

        /// <summary>Zero until the video's size is known.</summary>
        internal float Aspect => m_Player != null && m_Player.height > 0
            ? (float)m_Player.width / m_Player.height
            : 0;

        internal string Dimensions => m_Player != null && m_Player.height > 0
            ? $"{m_Player.width}x{m_Player.height}"
            : string.Empty;

        /// <summary>Zero until the video has opened.</summary>
        internal double FrameRate => m_Player != null ? m_Player.frameRate : 0;

        /// <summary>Zero until the video has opened.</summary>
        internal long FrameCount => m_Player != null ? (long)m_Player.frameCount : 0;

        /// <summary>Where <paramref name="frame"/> starts, in seconds. Zero until the video has opened.</summary>
        internal double TimeOf(long frame) => FrameRate > 0 ? frame / FrameRate : 0;

        /// <summary>Zero until the video has opened.</summary>
        internal double Length => TimeOf(FrameCount);

        /// <summary>The frame on screen, counted from zero.</summary>
        internal long Frame
        {
            get
            {
                if (m_Player == null)
                    return 0;
                // macOS rewinds the clock of a video paused at its end.
                if (m_State == State.Paused && m_ResumeFrom >= 0)
                    return m_ResumeFrom;
                return Math.Max(0, m_Player.frame);
            }
        }

        void MoveTo(long frame)
        {
            m_Player.frame = frame;
        }

        /// <summary>
        /// Seeks, which lands asynchronously. A drag sends seek after seek, and each one
        /// moves the frame being waited for.
        /// </summary>
        internal void SeekTo(long frame)
        {
            if (m_Player == null)
                return;
            // Visible in the history rather than lost: a seek that did nothing looks
            // exactly like one that landed on the frame it left.
            if (!m_Player.canSetTime)
            {
                Note($"seek to {frame} refused: player cannot seek (prepared {m_Player.isPrepared})");
                return;
            }

            frame = Math.Max(0, Math.Min(frame, FrameCount - 1));

            switch (m_State)
            {
                case State.Playing:
                case State.Seeking:
                    if (m_State == State.Playing)
                        m_SeekFrom = Frame;
                    m_SeekRetries = 0;
                    MoveTo(frame);
                    SetState(State.Seeking, $"seek to {frame}");
                    m_SeekTarget = frame;
                    break;
                case State.Paused:
                case State.SeekingToPause:
                case State.PausingOnFrame:
                    // Taken before anything moves, and kept across a drag's later seeks.
                    if (m_State == State.Paused)
                        m_SeekFrom = Frame;
                    m_SeekRetries = 0;
                    // Set before Play, which can land the seek straight away.
                    SetState(State.SeekingToPause, $"seek to {frame}");
                    m_SeekTarget = frame;
                    // Play first, seek second, as resuming does. A video paused at its
                    // end counts as finished, and Play starts it over from the beginning
                    // - dropping a seek made before it. The frames of that restart are
                    // before the target, and FrameReady lets them pass.
                    if (!m_Player.isPlaying)
                        m_Player.Play();
                    MoveTo(frame);
                    break;
            }
        }

        /// <summary>
        /// Steps one frame while paused, counting from where a seek still in flight is
        /// going rather than from where the player still is. Stops at either end
        /// rather than wrapping, so holding back stays on the first frame.
        /// </summary>
        internal void Step(int direction)
        {
            var to = StepTarget(direction);
            if (to >= 0)
                SeekTo(to);
        }

        /// <summary>Where a step would go, or -1 at that end of the video.</summary>
        long StepTarget(int direction)
        {
            var from = m_SeekTarget >= 0 ? m_SeekTarget : Frame;
            var to = from + direction;
            return to >= 0 && to < FrameCount ? to : -1;
        }

        /// <summary>
        /// Whether this file, as it is on disk now, is what is loaded. When it is,
        /// loading it again would only lose where it was and whether it was paused.
        /// </summary>
        internal bool IsShowing(string path)
        {
            return !string.IsNullOrEmpty(path) && path == m_Path && File.Exists(path)
                && File.GetLastWriteTimeUtc(path) == m_PathWriteTime;
        }

        internal void TogglePlay()
        {
            if (m_Player == null)
                return;

            switch (m_State)
            {
                case State.Playing:
                    m_Player.Pause();
                    SetState(State.Paused, "pause");
                    break;
                // The seek still has to land, and pausing before it does would keep
                // the old frame - so it pauses on the frame the seek brings.
                case State.Seeking:
                    SetState(State.SeekingToPause, "pause");
                    break;
                // Already playing underneath: carrying on is all it takes.
                case State.SeekingToPause:
                    SetState(State.Seeking, "play");
                    break;
                case State.PausingOnFrame:
                    SetState(State.Playing, "play");
                    break;
                case State.Paused:
                    var resumeFrom = m_ResumeFrom;
                    m_Player.Play();
                    if (resumeFrom >= 0)
                    {
                        SetState(State.Seeking, $"play from {resumeFrom}");
                        // The player's clock ran on while paused, so that is where it seeks from.
                        m_SeekFrom = Math.Max(0, m_Player.frame);
                        m_SeekTarget = resumeFrom;
                        m_SeekRetries = 0;
                        MoveTo(resumeFrom);
                    }
                    else
                    {
                        SetState(State.Playing, "play");
                    }
                    break;
            }
        }

        /// <summary>Pauses for a drag of the slider, remembering whether to play on after it.</summary>
        internal void BeginDrag()
        {
            m_ResumeAfterDrag = IsPlaying();
            if (m_ResumeAfterDrag)
                TogglePlay();
        }

        /// <summary>Plays on from wherever the drag left the video, if it was playing before.</summary>
        internal void EndDrag()
        {
            if (m_ResumeAfterDrag && !IsPlaying())
                TogglePlay();
            m_ResumeAfterDrag = false;
        }

        /// <summary>
        /// The play toggle, and under it the seek slider with the time, drawn
        /// from <paramref name="y"/> down within <paramref name="rc"/>. Whatever does not
        /// fit is left out, and <paramref name="y"/> ends below what was drawn.
        /// </summary>
        internal void DoControlsGUI(Rect rc, ref float y)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (y + height > rc.yMax)
                return;

            // A toggle, pressed while playing, as the inspector draws it.
            var playing = IsPlaying();
            var x = rc.x;
            var buttonRect = ButtonRect(rc, ref x, y, height, Styles.Play);
            if (GUI.Toggle(buttonRect, playing, playing ? Styles.Pause : Styles.Play, EditorStyles.miniButton) != playing)
                TogglePlay();

            // Stepping is for looking at one frame at a time, which playing moves on from.
            var canStep = !playing && m_State != State.Closed && m_State != State.Failed && FrameCount > 1;
            x += kButtonGap;
            EditorGUI.BeginDisabledGroup(!canStep || StepTarget(-1) < 0);
            if (GUI.Button(ButtonRect(rc, ref x, y, height, Styles.StepBack), Styles.StepBack, EditorStyles.miniButtonLeft))
                Step(-1);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!canStep || StepTarget(1) < 0);
            if (GUI.Button(ButtonRect(rc, ref x, y, height, Styles.StepForward), Styles.StepForward, EditorStyles.miniButtonRight))
                Step(1);
            EditorGUI.EndDisabledGroup();

            // The time, in a box at the right end of the row, so the slider gets the full width.
            var frameCount = FrameCount;
            var frame = Frame;
            // Minutes only when the recording runs that long, so both times fit the box.
            var minutes = Length >= 60;
            var length = frameCount > 0 ? FormatTime(Length, minutes) : "-.--";
            var time = frameCount > 0 ? $"{FormatTime(TimeOf(frame), minutes)} / {length}" : "-.-- / -.--";
            // Sized to the longest the text gets, so the box does not change width as it plays.
            var timeWidth = Mathf.Ceil(Styles.FrameCounter.CalcSize(new GUIContent($"{length} / {length}")).x) + 2 * kTimePadding;
            var timeX = Mathf.Max(x + kButtonGap, rc.xMax - timeWidth);
            var timeRect = new Rect(timeX, y, Mathf.Max(0, rc.xMax - timeX), height);
            GUI.Box(timeRect, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(timeRect.x + kTimePadding / 2, timeRect.y, Mathf.Max(0, timeRect.width - kTimePadding), height),
                time, Styles.FrameCounter);
            y += height;

            // Its own row, the whole width of the column.
            var boxHeight = height + 2 * kSeekPadding;
            if (y + kSeekGap + boxHeight > rc.yMax)
                return;
            y += kSeekGap;
            var boxRect = new Rect(rc.x, y, rc.width, boxHeight);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            var sliderRect = new Rect(boxRect.x + kSeekPadding, boxRect.y + kSeekPadding,
                Mathf.Max(0, boxRect.width - 2 * kSeekPadding), height);

            // The slider takes the mouse on the press and lets go on the release, which
            // can be outside this window - so a drag ends when it is no longer hot,
            // whatever event that turns up on. Paused before the slider handles the
            // press, so even the press's own seek is a paused one.
            if (m_DragControl != 0 && GUIUtility.hotControl != m_DragControl)
            {
                m_DragControl = 0;
                EndDrag();
            }
            var e = Event.current;
            var pressed = GUI.enabled && frameCount > 1 && e.type == EventType.MouseDown && e.button == 0
                && sliderRect.Contains(e.mousePosition);
            if (pressed)
                BeginDrag();

            // While dragging, the thumb stays where it was dragged to rather than going
            // back to the frame the player has caught up to so far.
            var thumb = TimeOf(m_DragControl != 0 && m_SeekTarget >= 0 ? m_SeekTarget : frame);

            EditorGUI.BeginDisabledGroup(frameCount <= 1);
            EditorGUI.BeginChangeCheck();
            var picked = GUI.HorizontalSlider(sliderRect, (float)thumb, 0, (float)TimeOf(Math.Max(0, frameCount - 1)));
            if (EditorGUI.EndChangeCheck())
                SeekTo((long)Math.Round(picked * FrameRate));
            EditorGUI.EndDisabledGroup();

            if (pressed)
            {
                if (GUIUtility.hotControl != 0)
                    m_DragControl = GUIUtility.hotControl;
                else
                    EndDrag();
            }
            y += boxHeight;

            if (Unsupported.IsDeveloperMode())
                DoDebugGUI(rc, ref y);
        }

        /// <summary>Seconds and hundredths, such as 5.37, or with minutes, such as 1:05.37.</summary>
        internal static string FormatTime(double seconds, bool minutes = true)
        {
            var hundredths = (long)Math.Round(Math.Max(0, seconds) * 100);
            return minutes
                ? $"{hundredths / 6000}:{hundredths / 100 % 60:00}.{hundredths % 100:00}"
                : $"{hundredths / 100}.{hundredths % 100:00}";
        }

        /// <summary>
        /// The next button along the row: icon sized when there is an icon, as wide as
        /// its text when the icon could not be found. Clipped to the column.
        /// </summary>
        static Rect ButtonRect(Rect rc, ref float x, float y, float height, GUIContent content)
        {
            var width = content.image != null
                ? kPlayButtonWidth
                : EditorStyles.miniButton.CalcSize(content).x;
            var rect = new Rect(x, y, Mathf.Max(0, Mathf.Min(width, rc.xMax - x)), height);
            x = rect.xMax;
            return rect;
        }

        /// <summary>
        /// Developer mode only: the state, what the player itself says, and how the
        /// state got here, newest first. Each row's tooltip has the full transition.
        /// </summary>
        void DoDebugGUI(Rect rc, ref float y)
        {
            y += kDebugGap;
            // Headed the way the live view heads its own debugging details.
            if (!AndroidLogcatStatsColumn.DeveloperModeGUI(rc, ref y))
                return;
            var height = EditorGUIUtility.singleLineHeight;

            foreach (var line in DebugStatus())
                DebugLine(rc, ref y, line, null);

            // Clear Events and Copy State, joined, under the list.
            var buttonsY = rc.yMax - height;
            if (buttonsY < y)
                return;
            DoHistoryGUI(new Rect(rc.x, y, rc.width, buttonsY - y));
            y = rc.yMax;
            var clearWidth = EditorStyles.miniButtonLeft.CalcSize(Styles.ClearHistory).x;
            if (GUI.Button(new Rect(rc.x, buttonsY, clearWidth, height), Styles.ClearHistory, EditorStyles.miniButtonLeft))
            {
                m_History.Clear();
                m_HistoryScroll = Vector2.zero;
            }
            var copyWidth = EditorStyles.miniButtonRight.CalcSize(Styles.CopyDebugInfo).x;
            if (GUI.Button(new Rect(rc.x + clearWidth, buttonsY, copyWidth, height), Styles.CopyDebugInfo,
                EditorStyles.miniButtonRight))
                EditorGUIUtility.systemCopyBuffer = GetDebugInfo();
        }

        /// <summary>What the panel shows above the history, one line each.</summary>
        IEnumerable<string> DebugStatus()
        {
            yield return $"State: {m_State}";
            if (m_Player == null)
                yield break;
            var player = m_Player.isPlaying ? "playing" : m_Player.isPaused ? "paused" : "stopped";
            yield return $"Player: {player}, prepared {m_Player.isPrepared}, frame {m_Player.frame}, time {m_Player.time:0.000}s";
            yield return $"Shown: {Frame} of {FrameCount} at {FrameRate:0.###} fps, resume {m_ResumeFrom}, " +
                $"seek {m_SeekFrom} -> {m_SeekTarget}";
        }

        /// <summary>
        /// Everything the panel shows, plus what it has no room for, as plain text to
        /// paste into a bug report: the file and the whole history oldest first, with
        /// from and to.
        /// </summary>
        internal string GetDebugInfo()
        {
            var info = new StringBuilder();
            info.AppendLine($"Video: {(string.IsNullOrEmpty(m_Path) ? "-" : Path.GetFileName(m_Path))}");
            foreach (var line in DebugStatus())
                info.AppendLine(line);

            info.AppendLine("State changes, oldest first:");
            foreach (var t in m_History)
                info.AppendLine($"  {t.Time:HH:mm:ss.fff} {t.From} -> {t.To}: {t.Reason}");
            return info.ToString();
        }

        /// <summary>
        /// The state changes, newest first, in whatever is left of the column. Each
        /// row's tooltip has the full transition. Rows are as wide as their text, and
        /// scroll sideways when the column is narrower.
        /// </summary>
        void DoHistoryGUI(Rect rc)
        {
            var rowHeight = EditorGUIUtility.singleLineHeight;
            if (rc.height < rowHeight * kMinHistoryRows)
                return;

            var transitions = m_History.ToArray();
            var rows = new GUIContent[transitions.Length];
            var contentWidth = 0f;
            for (var i = 0; i < transitions.Length; i++)
            {
                var t = transitions[transitions.Length - 1 - i];
                rows[i] = new GUIContent($"{t.Time:ss.fff} {t.To} ({t.Reason})",
                    $"{t.Time:HH:mm:ss.fff} {t.From} -> {t.To}: {t.Reason}");
                contentWidth = Mathf.Max(contentWidth, EditorStyles.miniLabel.CalcSize(rows[i]).x);
            }

            GUI.Box(rc, GUIContent.none, EditorStyles.helpBox);
            var view = new Rect(rc.x + kSeekPadding, rc.y + kSeekPadding,
                Mathf.Max(0, rc.width - 2 * kSeekPadding), Mathf.Max(0, rc.height - 2 * kSeekPadding));
            var content = new Rect(0, 0, Mathf.Max(contentWidth, view.width - kScrollbarWidth),
                rows.Length * rowHeight);

            m_HistoryScroll = GUI.BeginScrollView(view, m_HistoryScroll, content);
            for (var i = 0; i < rows.Length; i++)
                GUI.Label(new Rect(0, i * rowHeight, content.width, rowHeight), rows[i], EditorStyles.miniLabel);
            GUI.EndScrollView();
        }

        static void DebugLine(Rect rc, ref float y, string text, string tooltip)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (y + height > rc.yMax)
                return;
            GUI.Label(new Rect(rc.x, y, rc.width, height), new GUIContent(text, tooltip), EditorStyles.miniLabel);
            y += height;
        }
    }
}
