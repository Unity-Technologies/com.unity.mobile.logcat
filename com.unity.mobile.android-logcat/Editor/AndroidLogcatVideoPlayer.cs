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
        }

        // Square-ish, sized to the icon rather than stretched across the column.
        const float kPlayButtonWidth = 32;
        const float kSeekGap = 2;
        const float kSeekPadding = 2;

        private VideoPlayer m_Player;
        private GameObject m_PlayerGO;

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

        private void ErrorReceived(VideoPlayer source, string message)
        {
            // Stop video manually, otherwise it will spam Editor console.
            AndroidLogcatInternalLog.Log($"Error received while playing video, stopping video.\n{message}");
            Error = message;
            EndPauseAfterSeek();
            source.Stop();
        }

        private void SeekCompleted(VideoPlayer source)
        {
            m_SeekLanded = true;
            // A paused seek is not over until its frame is in - see FrameReady.
            if (!m_PauseAfterSeek)
                IsSeeking = false;
        }

        /// <summary>
        /// Only sent while a paused seek is waiting for its frame. Seek completion alone
        /// is too early: the texture still holds the old frame then, and pausing there
        /// would keep it.
        /// </summary>
        private void FrameReady(VideoPlayer source, long frameIdx)
        {
            if (!m_PauseAfterSeek || !m_SeekLanded)
                return;
            EndPauseAfterSeek();
            source.Pause();
            m_ResumeFrom = frameIdx;
        }

        private void EndPauseAfterSeek()
        {
            m_PauseAfterSeek = false;
            IsSeeking = false;
            if (m_Player != null)
                m_Player.sendFrameReadyEvents = false;
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
        }

        /// <summary>Why the current video could not be played, empty while it can.</summary>
        internal string Error { get; private set; } = string.Empty;

        public void Play(string path)
        {
            Error = string.Empty;
            EndPauseAfterSeek();
            m_ResumeFrom = -1;
            m_Player.Stop();
            m_Path = path;
            m_PathWriteTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;

            if (string.IsNullOrEmpty(path))
                return;
            if (!File.Exists(path))
                return;
            if (m_Player == null)
            {
                AndroidLogcatInternalLog.Log($"Cannot play '{path}', video player was not created ?");
                return;
            }

            m_Player.url = path;
            m_Player.Play();
        }

        /// <summary>
        /// False while a paused video is playing only to land a seek - it is still
        /// paused as far as anyone looking at it is concerned.
        /// </summary>
        public bool IsPlaying()
        {
            if (m_Player == null)
                return false;
            return m_Player.isPlaying && !m_PauseAfterSeek;
        }

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

        /// <summary>
        /// The frame on screen, counted from zero. Setting it seeks, which lands
        /// asynchronously - see <see cref="IsSeeking"/>.
        /// </summary>
        internal long Frame
        {
            get => m_Player != null ? Math.Max(0, m_Player.frame) : 0;
            private set
            {
                if (m_Player == null || !m_Player.canSetTime)
                    return;
                IsSeeking = true;
                m_SeekLanded = false;
                m_Player.frame = value;

                // A paused player does not decode the frame it seeks to, so the view
                // would keep showing the old one. It plays until that frame is in, and
                // FrameReady pauses it again. A drag sends seek after seek, and each one
                // moves the frame being waited for.
                if (!m_Player.isPlaying)
                {
                    m_PauseAfterSeek = true;
                    m_Player.sendFrameReadyEvents = true;
                    m_Player.Play();
                }
            }
        }

        bool m_PauseAfterSeek;
        bool m_SeekLanded;

        // Where a paused seek stopped, for Play to start from. Paused from inside
        // FrameReady, the player's clock carries on regardless, and resuming would
        // jump ahead by however long it sat paused. -1 when there is nothing to undo.
        long m_ResumeFrom = -1;

        string m_Path;
        DateTime m_PathWriteTime;

        /// <summary>
        /// Whether this file, as it is on disk now, is what is loaded. When it is,
        /// loading it again would only lose where it was and whether it was paused.
        /// </summary>
        internal bool IsShowing(string path)
        {
            return !string.IsNullOrEmpty(path) && path == m_Path && File.Exists(path)
                && File.GetLastWriteTimeUtc(path) == m_PathWriteTime;
        }

        /// <summary>
        /// A seek has been asked for and its frame is not in yet. A paused video draws
        /// nothing new on its own, so whoever shows it has to keep repainting until then.
        /// </summary>
        internal bool IsSeeking { get; private set; }

        private void TogglePlay()
        {
            if (m_Player == null)
                return;

            // Resuming mid seek just means not pausing once it lands.
            if (m_PauseAfterSeek)
                EndPauseAfterSeek();
            else if (m_Player.isPlaying)
                m_Player.Pause();
            else
            {
                m_Player.Play();
                if (m_ResumeFrom >= 0)
                {
                    m_Player.frame = m_ResumeFrom;
                    m_ResumeFrom = -1;
                }
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
                Frame = (long)Mathf.Round(picked);
            EditorGUI.EndDisabledGroup();
            GUI.Label(counterRect, counter, Styles.FrameCounter);
            y += boxHeight;
        }
    }
}
