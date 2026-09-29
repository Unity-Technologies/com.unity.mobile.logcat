using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatVideoPlayer : IDisposable
    {
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
        }

        private void ErrorReceived(VideoPlayer source, string message)
        {
            // Stop video manually, otherwise it will spam Editor console.
            AndroidLogcatInternalLog.Log($"Error received while playing video, stopping video.\n{message}");
            source.Stop();
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

        public void Play(string path)
        {
            m_Player.Stop();

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

        public bool IsPlaying()
        {
            if (m_Player == null)
                return false;
            return m_Player.isPlaying;
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

        internal string Length => m_Player != null && m_Player.length > 0
            ? $"{m_Player.length:0.00} s"
            : string.Empty;

        internal void TogglePlay()
        {
            if (m_Player == null)
                return;

            if (m_Player.isPlaying)
                m_Player.Pause();
            else
                m_Player.Play();
        }
    }
}
