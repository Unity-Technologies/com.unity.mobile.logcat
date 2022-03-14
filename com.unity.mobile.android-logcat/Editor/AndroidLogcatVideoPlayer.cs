using System;
using System.Collections.Generic;
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

        public Rect GetVideoRect()
        {
            if (m_Player == null)
            {
                AndroidLogcatInternalLog.Log($"Cannot get video rect, video player is not created?");
                return Rect.zero;
            }

            if (m_Player.width == 0 || m_Player.height == 0)
                return Rect.zero;

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

        private void DoVideoInfoGUI(Rect rc)
        {
            var v = m_Player;
            var info = new List<string>();
            info.Add($" Dimensions: {v.width} x {v.height}");
            info.Add($" Length: {v.length:0.00} seconds.");
            info.Add($" Frame Count: {v.frameCount}");
            info.Add($" Frame: {v.frame}");

            var infoRC = new Rect(Screen.width - 200, rc.y, 200, info.Count * 19);
            GUI.Box(infoRC, GUIContent.none, GUI.skin.window);
            GUI.Label(infoRC, string.Join("\n", info));
        }

        public void DoGUI()
        {
            var rc = GetVideoRect();
            if (m_Player != null && m_Player.texture != null)
            {
                GUI.DrawTexture(rc, m_Player.texture);
                DoVideoInfoGUI(rc);
            }
            else
            {
                EditorGUILayout.HelpBox("No video to show.", MessageType.Info);
            }
        }
    }
}
