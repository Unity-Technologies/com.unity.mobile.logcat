#if PLATFORM_ANDROID
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;
using Unity.Jobs;
using UnityEngine;
using System.Threading;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatIntegrationTask
    {
        internal AndroidLogcatTaskResult result;
        internal Action<AndroidLogcatTaskResult> integrateAction;
    }
}
#endif
