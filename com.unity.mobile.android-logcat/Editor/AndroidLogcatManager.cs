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
    internal class AndroidLogcatManager : ScriptableSingleton<AndroidLogcatManager>
    {
        private IAndroidLogcatRuntime m_Runtime;

        internal void OnEnable()
        {
            m_Runtime = new AndroidLogcatRuntime();
            m_Runtime.Initialize();
        }

        internal void OnDisable()
        {
            m_Runtime.Shutdown();
            m_Runtime = null;
        }

        internal IAndroidLogcatRuntime Runtime
        {
            get
            {
                return m_Runtime;
            }
        }
    }
}
#endif
