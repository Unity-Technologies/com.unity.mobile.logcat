using UnityEditor;


namespace Unity.Android.Logcat
{
    internal class AndroidLogcatManager : ScriptableSingleton<AndroidLogcatManager>
    {
        private AndroidLogcatRuntimeBase m_Runtime;

        internal void OnEnable()
        {
            Initialize();
        }

        internal void OnDisable()
        {
            if (m_Runtime != null)
            {
                m_Runtime.Shutdown();
                m_Runtime = null;
            }
        }

        private void Initialize()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime != null)
                return;

            m_Runtime = new AndroidLogcatRuntime();
            m_Runtime.Initialize();
        }

        internal AndroidLogcatRuntimeBase Runtime
        {
            get
            {
                Initialize();
                return m_Runtime;
            }
        }
    }
}
