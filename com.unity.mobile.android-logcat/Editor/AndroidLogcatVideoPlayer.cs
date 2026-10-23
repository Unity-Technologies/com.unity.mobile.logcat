using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Video;

namespace Unity.Android.Logcat
{
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

            internal static readonly GUIContent DeveloperMode = new GUIContent("Developer Mode");
        }

        // Square-ish, sized to the icon rather than stretched across the column.
        const float kPlayButtonWidth = 32;
        const float kSeekGap = 2;
        const float kSeekPadding = 2;
        const float kDebugGap = 5;
        // Several seeks' worth, scrolled through rather than all on screen.
        const int kHistoryLength = 100;
        const float kScrollbarWidth = 16;
        // The history is only worth drawing if a few rows of it fit.
        const int kMinHistoryRows = 3;

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
            /// <summary>Paused when the seek was asked for: plays until it lands, then pauses on its frame.</summary>
            SeekingToPause,
            /// <summary>Playing only until the next frame is in, then pausing on it.</summary>
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

        // Where a paused video stopped, for Play to start from. Paused from inside
        // FrameReady, the player's clock carries on regardless, and resuming would
        // jump ahead by however long it sat paused. -1 when there is nothing to undo.
        long m_ResumeFrom = -1;

        string m_Path;
        DateTime m_PathWriteTime;

        Vector2 m_HistoryScroll;

        internal AndroidLogcatVideoPlayer()
        {
            var name = "LogcatVideoPlayer";
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

            m_History.Enqueue(new Transition { Time = DateTime.Now, From = m_State, To = state, Reason = reason });
            while (m_History.Count > kHistoryLength)
                m_History.Dequeue();

            m_State = state;
            if (state != State.Paused)
                m_ResumeFrom = -1;
            if (m_Player != null)
                m_Player.sendFrameReadyEvents = state == State.SeekingToPause || state == State.PausingOnFrame;
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
                // Not paused yet: the texture still holds the old frame until the next
                // one is decoded, and pausing now would keep it.
                case State.SeekingToPause:
                    SetState(State.PausingOnFrame, "seek landed");
                    break;
            }
        }

        private void FrameReady(VideoPlayer source, long frameIdx)
        {
            if (m_State != State.PausingOnFrame)
                return;
            source.Pause();
            SetState(State.Paused, $"frame {frameIdx} in");
            m_ResumeFrom = frameIdx;
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
        /// Opens a video paused on its first frame, or closes the current one when
        /// <paramref name="path"/> is empty.
        /// </summary>
        public void Open(string path)
        {
            Error = string.Empty;
            m_Path = path;
            m_PathWriteTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;

            if (m_Player == null)
            {
                AndroidLogcatInternalLog.Log($"Cannot open '{path}', video player was not created ?");
                SetState(State.Closed, "no player");
                return;
            }

            m_Player.Stop();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SetState(State.Closed, "closed");
                return;
            }

            m_Player.url = path;
            // There is no seek to wait for, only the first frame.
            SetState(State.PausingOnFrame, $"opened {Path.GetFileName(path)}");
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
        internal long FrameCount => m_Player != null ? (long)m_Player.frameCount : 0;

        /// <summary>The frame on screen, counted from zero.</summary>
        internal long Frame => m_Player != null ? Math.Max(0, m_Player.frame) : 0;

        /// <summary>
        /// Seeks, which lands asynchronously. A drag sends seek after seek, and each one
        /// moves the frame being waited for.
        /// </summary>
        private void SeekTo(long frame)
        {
            if (m_Player == null || !m_Player.canSetTime)
                return;

            switch (m_State)
            {
                case State.Playing:
                case State.Seeking:
                    m_Player.frame = frame;
                    SetState(State.Seeking, $"seek to {frame}");
                    break;
                case State.Paused:
                case State.SeekingToPause:
                case State.PausingOnFrame:
                    // Set before Play, which can land the seek straight away.
                    SetState(State.SeekingToPause, $"seek to {frame}");
                    m_Player.frame = frame;
                    if (!m_Player.isPlaying)
                        m_Player.Play();
                    break;
            }
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

        private void TogglePlay()
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
                        m_Player.frame = resumeFrom;
                    }
                    else
                    {
                        SetState(State.Playing, "play");
                    }
                    break;
            }
        }

        /// <summary>
        /// The play toggle, and under it the seek slider with the frame counter, drawn
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
            var width = Styles.Play.image != null ? Mathf.Min(kPlayButtonWidth, rc.width) : rc.width;
            var buttonRect = new Rect(rc.x, y, width, height);
            if (GUI.Toggle(buttonRect, playing, playing ? Styles.Pause : Styles.Play, EditorStyles.miniButton) != playing)
                TogglePlay();
            y += height;

            // Its own row: the column is too narrow to share one with the toggle.
            var boxHeight = height + 2 * kSeekPadding;
            if (y + kSeekGap + boxHeight > rc.yMax)
                return;
            y += kSeekGap;
            var boxRect = new Rect(rc.x, y, rc.width, boxHeight);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            var frameCount = FrameCount;
            var frame = Frame;
            // Counted from one, so the last frame reads as the total. Measured against
            // the widest it can get, so the slider does not jitter as the number grows.
            var counter = frameCount > 0 ? $"{frame + 1}/{frameCount}" : "-/-";
            var counterWidth = EditorStyles.miniLabel.CalcSize(new GUIContent($"{frameCount}/{frameCount}")).x;
            var inner = new Rect(boxRect.x + kSeekPadding, boxRect.y + kSeekPadding,
                boxRect.width - 2 * kSeekPadding, height);
            var sliderRect = new Rect(inner.x, inner.y, Mathf.Max(0, inner.width - counterWidth - kSeekPadding), height);
            var counterRect = new Rect(inner.xMax - counterWidth, inner.y, counterWidth, height);

            EditorGUI.BeginDisabledGroup(frameCount <= 1);
            EditorGUI.BeginChangeCheck();
            var picked = GUI.HorizontalSlider(sliderRect, frame, 0, Mathf.Max(0, frameCount - 1));
            if (EditorGUI.EndChangeCheck())
                SeekTo((long)Mathf.Round(picked));
            EditorGUI.EndDisabledGroup();
            GUI.Label(counterRect, counter, Styles.FrameCounter);
            y += boxHeight;

            if (Unsupported.IsDeveloperMode())
                DoDebugGUI(rc, ref y);
        }

        /// <summary>
        /// Developer mode only: the state, what the player itself says, and how the
        /// state got here, newest first. Each row's tooltip has the full transition.
        /// </summary>
        void DoDebugGUI(Rect rc, ref float y)
        {
            y += kDebugGap;
            // Headed the way the live view heads its own debugging details.
            var height = EditorGUIUtility.singleLineHeight;
            if (y + height > rc.yMax)
                return;
            GUI.Label(new Rect(rc.x, y, rc.width, height), Styles.DeveloperMode, EditorStyles.miniBoldLabel);
            y += height;

            DebugLine(rc, ref y, $"State: {m_State}", null);
            if (m_Player != null)
            {
                var player = m_Player.isPlaying ? "playing" : m_Player.isPaused ? "paused" : "stopped";
                DebugLine(rc, ref y, $"Player: {player}, frame {m_Player.frame}, resume {m_ResumeFrom}", null);
            }

            DoHistoryGUI(new Rect(rc.x, y, rc.width, rc.yMax - y));
            y = rc.yMax;
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
