#if PLATFORM_ANDROID
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;
using static Unity.Android.Logcat.AndroidLogcatConsoleWindow;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatProjectSettings
    {
        [SerializeField]
        private string m_SelectedDeviceId;
        [SerializeField]
        private PackageInformation m_SelectedPackage;
        [SerializeField]
        private AndroidLogcat.Priority m_SelectedPriority;
        private Dictionary<string, List<PackageInformation>> m_KnownPackages;
        [SerializeField]
        private List<PackageInformation> m_KnownPackagesForSerialization;
        [SerializeField]
        private AndroidLogcatTagsControl m_TagControl;
        [SerializeField]
        private AndroidLogcatMemoryViewerState m_MemoryViewerState;

        public string LastSelectedDeviceId
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

        public bool LastSelectedDeviceIdValid
        {
            get
            {
                return !string.IsNullOrEmpty(m_SelectedDeviceId);
            }
        }

        public PackageInformation LastSelectedPackage
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

        public Dictionary<string, List<PackageInformation>> KnownPackages
        {
            set
            {
                m_KnownPackages = value;
            }
            get
            {
                return m_KnownPackages;
            }
        }

        private static List<PackageInformation> PackagesToList(Dictionary<string, List<PackageInformation>> packages)
        {
            var temp = new List<PackageInformation>();
            foreach (var p in packages)
            {
                temp.AddRange(p.Value);
            }
            return temp;
        }

        private static Dictionary<string, List<PackageInformation>> PackagesToDictionary(List<PackageInformation> allPackages)
        {
            var dictionaryPackages = new Dictionary<string, List<PackageInformation>>();
            foreach (var p in allPackages)
            {
                List<PackageInformation> packages;
                if (!dictionaryPackages.TryGetValue(p.deviceId, out packages))
                {
                    packages = new List<PackageInformation>();
                    dictionaryPackages[p.deviceId] = packages;
                }
                packages.Add(p);
            }

            return dictionaryPackages;
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
            m_KnownPackages = new Dictionary<string, List<PackageInformation>>();
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
                settings.m_KnownPackages = PackagesToDictionary(settings.m_KnownPackagesForSerialization);
                return settings;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Load Preferences from Json failed: " + ex.Message);
            }
            return null;
        }

        internal static void Save(AndroidLogcatProjectSettings settings, string path, AndroidLogcatRuntimeBase runtime)
        {
            if (settings == null)
                throw new NullReferenceException(nameof(settings));

            var selectedDevice = runtime.DeviceQuery.SelectedDevice;
            settings.LastSelectedDeviceId = selectedDevice != null ? selectedDevice.Id : "";
            settings.m_KnownPackagesForSerialization = PackagesToList(settings.m_KnownPackages);

            var jsonString = JsonUtility.ToJson(settings, true);
            if (string.IsNullOrEmpty(jsonString))
                return;

            File.WriteAllText(path, jsonString);
        }
    }
}

#endif
