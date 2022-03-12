using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine.Video;
using Debug = System.Diagnostics.Debug;

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
                m_PlayerGO = new GameObject(name);
                //TODO
                //go.hideFlags = HideFlags.HideAndDontSave;
                m_PlayerGO.hideFlags = HideFlags.DontSave;
            }

            m_Player = m_PlayerGO.GetComponent<VideoPlayer>();
            if (m_Player == null)
                m_Player = m_PlayerGO.AddComponent<VideoPlayer>();
            m_Player.renderMode = VideoRenderMode.APIOnly;
            m_Player.isLooping = true;
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
                GameObject.Destroy(m_PlayerGO);
                m_PlayerGO = null;
            }
        }

        public void Play(string path)
        {
            if (m_Player == null)
            {
                AndroidLogcatInternalLog.Log($"Cannot play '{path}', video player was not created ?");
                return;
            }

            m_Player.url = path;
            m_Player.Play();
        }

        public Rect GetVideoRect()
        {
            if (m_Player == null)
            {
                AndroidLogcatInternalLog.Log($"Cannot get video rect, video player is not created?");
                return Rect.zero;
            }

            float aspect = (float)m_Player.width / (float)m_Player.height;
            var rc = GUILayoutUtility.GetAspectRect(aspect);
            Rect r1, r2;

            var correctedHeight = Screen.height - rc.y - 20;
            var s = correctedHeight / rc.height;
            r1 = rc;
            r1.width *= s;
            r1.height *= s;

            var correctedWidth = Screen.width - rc.x;
            s = correctedWidth / rc.width;
            r2 = rc;
            r2.width *= s;
            r2.height *= s;

            var videoRect = r1.width < r2.width ? r1 : r2;

            videoRect.x += (Screen.width - videoRect.width) * 0.5f;

            return videoRect;
        }

        public void DoGUI()
        {
            if (m_Player != null && m_Player.texture != null)
                GUI.DrawTexture(GetVideoRect(), m_Player.texture);
        }
    }
}
