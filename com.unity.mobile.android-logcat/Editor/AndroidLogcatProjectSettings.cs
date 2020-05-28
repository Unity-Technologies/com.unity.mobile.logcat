#if PLATFORM_ANDROID
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatProjectSettings
    {
        [SerializeField]
        private string m_SelectedDeviceId;
        [SerializeField]
        private AndroidLogcatConsoleWindow.PackageInformation m_SelectedPackage;
        [SerializeField]
        private AndroidLogcat.Priority m_SelectedPriority;
        [SerializeField]
        private List<AndroidLogcatConsoleWindow.PackageInformation> m_PackagesForSerialization;
        [SerializeField]
        private AndroidLogcatTagsControl m_TagControl;
        [SerializeField]
        private AndroidLogcatMemoryViewerState m_MemoryViewerState;

        public string SelectedDeviceId
        {
            set
            {
                m_SelectedDeviceId = value;
            }
            get
            {
                return m_SelectedDeviceId;
            }
        }

        public bool SelectedDeviceIdValid
        {
            get
            {
                return !string.IsNullOrEmpty(m_SelectedDeviceId);
            }
        }

        public AndroidLogcatConsoleWindow.PackageInformation SelectedPackage
        {
            set
            {
                m_SelectedPackage = value;
            }
            get
            {
                return m_SelectedPackage;
            }
        }

        public bool SelectedPackageValid
        {
            get
            {
                return m_SelectedPackage != null &&
                    !string.IsNullOrEmpty(m_SelectedPackage.deviceId) &&
                    m_SelectedPackage.processId > 0;
            }
        }


        public AndroidLogcat.Priority SelectedPriority
        {
            set
            {
                m_SelectedPriority = value;
            }
            get
            {
                return m_SelectedPriority;
            }
        }

        public List<AndroidLogcatConsoleWindow.PackageInformation> PackagesForSerialization
        {
            set
            {
                m_PackagesForSerialization = value;
            }
            get
            {
                return m_PackagesForSerialization;
            }
        }

        public AndroidLogcatTagsControl TagControl
        {
            set
            {
                m_TagControl = value;
            }
            get
            {
                return m_TagControl;
            }
        }

        public AndroidLogcatMemoryViewerState MemoryViewerState
        {
            set
            {
                m_MemoryViewerState = value;
            }
            get
            {
                return m_MemoryViewerState;
            }
        }

        internal AndroidLogcatProjectSettings()
        {
            Reset();
        }

        internal void Reset()
        {
            m_SelectedDeviceId = string.Empty;
            m_SelectedPriority = AndroidLogcat.Priority.Verbose;
            m_TagControl = new AndroidLogcatTagsControl();
            m_PackagesForSerialization = new List<AndroidLogcatConsoleWindow.PackageInformation>();
            m_MemoryViewerState = new AndroidLogcatMemoryViewerState();
        }

        internal static AndroidLogcatProjectSettings Load(string path)
        {
            if (!File.Exists(path))
                return null;

            var jsonString = File.ReadAllText(path);
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                var settings = new AndroidLogcatProjectSettings();
                JsonUtility.FromJsonOverwrite(jsonString, settings);
                return settings;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Load Preferences from Json failed: " + ex.Message);
            }
            return null;
        }

        internal static void Save(AndroidLogcatProjectSettings settings, string path)
        {
            if (settings == null)
                throw new NullReferenceException(nameof(settings));

            var jsonString = JsonUtility.ToJson(settings, true);
            if (string.IsNullOrEmpty(jsonString))
                return;

            File.WriteAllText(path, jsonString);
        }
    }
}

#endif
