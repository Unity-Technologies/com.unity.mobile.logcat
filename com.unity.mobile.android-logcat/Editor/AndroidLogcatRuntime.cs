#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;


namespace Unity.Android.Logcat
{
    internal interface IAndroidLogcatRuntime
    {
        AndroidLogcatDispatcher Dispatcher { get; }

        AndroidLogcatSettings Settings { get; }

        AndroidTools Tools { get; }

        AndroidLogcatDeviceQueryBase DeviceQuery { get; }

        IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction);

        void Initialize();

        void Shutdown();

        event Action Update;
    }

    internal class AndroidLogcatRuntime : IAndroidLogcatRuntime
    {
        private AndroidLogcatDispatcher m_Dispatcher;
        private AndroidLogcatSettings m_Settings;
        private AndroidTools m_Tools;
        private AndroidLogcatDeviceQuery m_DeviceQuery;

        public event Action Update;

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

            m_Tools = new AndroidTools();

            m_DeviceQuery = new AndroidLogcatDeviceQuery(this);
        }

        public void Shutdown()
        {
            AndroidLogcatSettings.Save(m_Settings);
            m_Settings = null;

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
