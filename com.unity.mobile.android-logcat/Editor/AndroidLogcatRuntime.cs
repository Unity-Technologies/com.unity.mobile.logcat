#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.IO;


namespace Unity.Android.Logcat
{
    internal interface IAndroidLogcatRuntime
    {
        AndroidLogcatDispatcher Dispatcher { get; }

        AndroidLogcatSettings Settings { get; }

        AndroidLogcatProjectSettings ProjectSettings { get; }

        AndroidTools Tools { get; }

        AndroidLogcatDeviceQueryBase DeviceQuery { get; }

        IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction);

        void Initialize();

        void Shutdown();

        event Action Update;
        event Action Closing;
    }

    internal class AndroidLogcatRuntime : IAndroidLogcatRuntime
    {
        private static readonly string kAndroidLogcatSettingsPath = Path.Combine("ProjectSettings", "AndroidLogcatSettings.asset");

        private AndroidLogcatDispatcher m_Dispatcher;
        private AndroidLogcatSettings m_Settings;
        private AndroidLogcatProjectSettings m_ProjectSettings;
        private AndroidTools m_Tools;
        private AndroidLogcatDeviceQuery m_DeviceQuery;

        public event Action Update;
        public event Action Closing;

        public IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId,
            Action<string> logCallbackAction)
        {
            return new AndroidLogcatMessageProvider(adb, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
        }

        public AndroidLogcatDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
        }

        public AndroidLogcatSettings Settings
        {
            get { return m_Settings; }
        }

        public AndroidLogcatProjectSettings ProjectSettings
        {
            get { return m_ProjectSettings; }
        }

        public AndroidTools Tools
        {
            get { return m_Tools; }
        }

        public AndroidLogcatDeviceQueryBase DeviceQuery
        {
            get { return m_DeviceQuery; }
        }

        public void Initialize()
        {
            EditorApplication.update += OnUpdate;

            m_Dispatcher = new AndroidLogcatDispatcher(this);
            m_Dispatcher.Initialize();

            m_Settings = AndroidLogcatSettings.Load();

            m_ProjectSettings = AndroidLogcatProjectSettings.Load(kAndroidLogcatSettingsPath);
            if (m_ProjectSettings == null)
            {
                m_ProjectSettings = new AndroidLogcatProjectSettings();
                m_ProjectSettings.Reset();
            }

            m_Tools = new AndroidTools();

            m_DeviceQuery = new AndroidLogcatDeviceQuery(this);
        }

        public void Shutdown()
        {
            Closing?.Invoke();
            // ProjectSettings is accessing some information from runtime during save
            AndroidLogcatProjectSettings.Save(m_ProjectSettings, kAndroidLogcatSettingsPath, this);
            AndroidLogcatSettings.Save(m_Settings);

            m_Settings = null;
            m_ProjectSettings = null;
            m_Dispatcher.Shutdown();
            m_Dispatcher = null;
            EditorApplication.update -= OnUpdate;
        }

        public void OnUpdate()
        {
            Update?.Invoke();
        }
    }
}
#endif
