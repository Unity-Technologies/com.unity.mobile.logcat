using System;
using UnityEditor;
using System.IO;


namespace Unity.Android.Logcat
{
    internal abstract class AndroidLogcatRuntimeBase
    {
        protected AndroidLogcatDispatcher m_Dispatcher;
        protected AndroidLogcatSettings m_Settings;
        protected AndroidLogcatUserSettings m_UserSettings;
        protected AndroidTools m_Tools;
        protected AndroidLogcatDeviceQueryBase m_DeviceQuery;
        protected AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        protected AndroidLogcatCaptureVideo m_CaptureVideo;
        protected bool m_Initialized;

        protected abstract string UserSettingsPath { get; }

        private void ValidateIsInitialized()
        {
            if (!m_Initialized)
                throw new Exception("Runtime is not initialized");
        }

        public AndroidLogcatDispatcher Dispatcher
        {
            get { ValidateIsInitialized(); return m_Dispatcher; }
        }

        public AndroidLogcatSettings Settings
        {
            get { ValidateIsInitialized(); return m_Settings; }
        }

        public AndroidLogcatUserSettings UserSettings
        {
            get { ValidateIsInitialized(); return m_UserSettings; }
        }

        public AndroidTools Tools
        {
            get { ValidateIsInitialized(); return m_Tools; }
        }

        public AndroidLogcatDeviceQueryBase DeviceQuery
        {
            get { ValidateIsInitialized(); return m_DeviceQuery; }
        }

        public AndroidLogcatCaptureVideo CaptureVideo
        {
            get { ValidateIsInitialized(); return m_CaptureVideo; }
        }

        public AndroidLogcatCaptureScreenshot CaptureScreenshot
        {
            get { ValidateIsInitialized(); return m_CaptureScreenshot; }
        }

        public abstract AndroidLogcatMessageProviderBase CreateMessageProvider(AndroidBridge.ADB adb, Priority priority, int processId, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction);
        protected abstract AndroidLogcatDeviceQueryBase CreateDeviceQuery();
        protected abstract AndroidLogcatSettings LoadEditorSettings();
        protected abstract AndroidTools CreateAndroidTools();
        protected abstract AndroidLogcatCaptureVideo CreateScreenRecorder();
        protected abstract AndroidLogcatCaptureScreenshot CreateScreenCapture();
        protected abstract void SaveEditorSettings(AndroidLogcatSettings settings);

        public virtual void Initialize()
        {
            m_Dispatcher = new AndroidLogcatDispatcher(this);
            m_Dispatcher.Initialize();

            m_Settings = LoadEditorSettings();

            Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath));
            m_UserSettings = AndroidLogcatUserSettings.Load(UserSettingsPath);
            if (m_UserSettings == null)
            {
                m_UserSettings = new AndroidLogcatUserSettings();
                m_UserSettings.Reset();
            }

            m_Tools = CreateAndroidTools();
            m_DeviceQuery = CreateDeviceQuery();
            m_CaptureVideo = CreateScreenRecorder();
            m_CaptureScreenshot = CreateScreenCapture();

            m_Initialized = true;
        }

        public virtual void Shutdown()
        {
            Closing?.Invoke();
            // ProjectSettings is accessing some information from runtime during save
            AndroidLogcatUserSettings.Save(m_UserSettings, UserSettingsPath, this);
            SaveEditorSettings(m_Settings);

            m_Initialized = false;
            m_Settings = null;
            m_UserSettings = null;
            m_Tools = null;
            m_CaptureVideo = null;
            m_Dispatcher.Shutdown();
            m_Dispatcher = null;
        }

        public void OnUpdate()
        {
            Update?.Invoke();
        }

        public event Action Update;
        public event Action Closing;
    }

    internal class AndroidLogcatRuntime : AndroidLogcatRuntimeBase
    {
        private static readonly string kUserSettingsPath = Path.Combine("UserSettings", "AndroidLogcatSettings.asset");

        protected override string UserSettingsPath { get => kUserSettingsPath; }

        private bool m_SubscribeToEditorUpdate;

        internal AndroidLogcatRuntime(bool subscribeToEditorUpdate = true)
        {
            m_SubscribeToEditorUpdate = subscribeToEditorUpdate;
        }

        public override AndroidLogcatMessageProviderBase CreateMessageProvider(AndroidBridge.ADB adb, Priority priority, int processId, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
        {
            return new AndroidLogcatMessageProvider(adb, priority, processId, logPrintFormat, device, logCallbackAction);
        }

        public override void Initialize()
        {
            if (m_SubscribeToEditorUpdate)
                EditorApplication.update += OnUpdate;
            base.Initialize();
        }

        public override void Shutdown()
        {
            base.Shutdown();
            if (m_SubscribeToEditorUpdate)
                EditorApplication.update -= OnUpdate;
        }

        protected override AndroidLogcatDeviceQueryBase CreateDeviceQuery()
        {
            return new AndroidLogcatDeviceQuery(this);
        }

        protected override AndroidTools CreateAndroidTools()
        {
            return new AndroidTools();
        }

        protected override AndroidLogcatCaptureVideo CreateScreenRecorder()
        {
            return new AndroidLogcatCaptureVideo(this);
        }

        protected override AndroidLogcatCaptureScreenshot CreateScreenCapture()
        {
            return new AndroidLogcatCaptureScreenshot(this);
        }

        protected override AndroidLogcatSettings LoadEditorSettings()
        {
            return AndroidLogcatSettings.Load();
        }

        protected override void SaveEditorSettings(AndroidLogcatSettings settings)
        {
            AndroidLogcatSettings.Save(settings);
        }
    }
}
