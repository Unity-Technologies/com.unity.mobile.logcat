using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCaptureWindow : EditorWindow
    {
        [SerializeField] private string m_ImagePath;
#if PLATFORM_ANDROID
        private Texture2D m_ImageTexture = null;
        bool didLoad = false;
        private const int kButtonAreaHeight = 30;
        private const int kSaveButtonWidth = 60;
        private const int kBottomAreaHeight = 8;

        public static void Show(string imagePath)
        {
            AndroidLogcatScreenCaptureWindow win = EditorWindow.GetWindow<AndroidLogcatScreenCaptureWindow>("Device Screen Capture");
            if (win.m_ImagePath != imagePath)
            {
                win.m_ImagePath = imagePath;
                win.didLoad = false;
            }
        }

        void OnGUI()
        {
            if (!didLoad)
            {
                LoadImage();
                didLoad = true;
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save...", GUILayout.Width(kSaveButtonWidth)))
            {
                var path = EditorUtility.SaveFilePanel("Save Screen Capture", "", Path.GetFileName(m_ImagePath), "png");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.Copy(m_ImagePath, path, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogErrorFormat("Failed to save to '{0}' as '{1}'.", path, ex.Message);
                    }
                }
            }
            GUILayout.Space(20);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (m_ImageTexture != null)
            {
                Rect rect = new Rect(0, kButtonAreaHeight, position.width, position.height - kButtonAreaHeight - kBottomAreaHeight);
                GUI.DrawTexture(rect, m_ImageTexture, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndVertical();
        }

        void LoadImage()
        {
            if (!File.Exists(m_ImagePath))
                return;

            byte[] imageData;
            imageData = File.ReadAllBytes(m_ImagePath);

            m_ImageTexture = new Texture2D(2, 2); // The size will be replaced by LoadImage().
            if (!m_ImageTexture.LoadImage(imageData))
                return;

            maxSize = new Vector2(Math.Max(m_ImageTexture.width, position.width), m_ImageTexture.height + kButtonAreaHeight);
        }

#else
        internal void OnGUI()
        {
            AndroidLogcatUtilities.ShowActivePlatformNotAndroidMessage();
        }

#endif
    }
}
