using System;
using UnityEngine;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatCommandEntry
    {
        [SerializeField] internal string name;
        [SerializeField] internal string command;

        internal AndroidLogcatCommandEntry() { }

        internal AndroidLogcatCommandEntry(string name, string command)
        {
            this.name = name;
            this.command = command;
        }
    }

    [Serializable]
    internal class AndroidLogcatCommandExportData
    {
        public AndroidLogcatCommandEntry[] favorites = Array.Empty<AndroidLogcatCommandEntry>();
        public AndroidLogcatCommandEntry[] general = Array.Empty<AndroidLogcatCommandEntry>();
    }
}
