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

        IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId, Action<string> logCallbackAction);

        void Initialize();

        void Shutdown();

        event Action OnUpdate;
    }

    internal class AndroidLogcatRuntime : IAndroidLogcatRuntime
    {
        private AndroidLogcatDispatcher m_Dispatcher;

        public event Action OnUpdate;

        public IAndroidLogcatMessageProvider CreateMessageProvider(ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, string deviceId,
            Action<string> logCallbackAction)
        {
            return new AndroidLogcatMessageProvider(adb, filter, priority, packageID, logPrintFormat, deviceId, logCallbackAction);
        }

        public AndroidLogcatDispatcher Dispatcher
        {
            get { return m_Dispatcher; }
        }

        public void Initialize()
        {
            EditorApplication.update += Update;

            m_Dispatcher = new AndroidLogcatDispatcher(this);
            m_Dispatcher.Initialize();
        }

        public void Shutdown()
        {
            m_Dispatcher.Shutdown();
            m_Dispatcher = null;

            EditorApplication.update -= Update;
        }

        public void Update()
        {
            if (OnUpdate != null)
                OnUpdate.Invoke();
        }
    }
}
#endif
