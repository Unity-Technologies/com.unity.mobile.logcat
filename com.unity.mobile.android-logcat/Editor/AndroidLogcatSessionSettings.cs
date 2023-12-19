using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Settings which only persist during the Editor session.
    /// </summary>
    internal static class AndroidLogcatSessionSettings
    {
        private static string GetName(string name)
        {
            return $"{nameof(AndroidLogcatSessionSettings)}.{name}";
        }

        internal static bool ShowTagPriorityErrors
        {
            set => SessionState.SetBool(GetName(nameof(ShowTagPriorityErrors)), value);
            get => SessionState.GetBool(GetName(nameof(ShowTagPriorityErrors)), true);
        }
    }
}
