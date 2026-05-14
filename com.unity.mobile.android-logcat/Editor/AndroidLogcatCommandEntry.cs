using System;
using System.Collections.Generic;
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
    internal class AndroidLogcatCommandEntryList
    {
        public AndroidLogcatCommandEntry[] items = Array.Empty<AndroidLogcatCommandEntry>();
    }

    [Serializable]
    internal class AndroidLogcatCommandExportData
    {
        public AndroidLogcatCommandEntry[] favorites = Array.Empty<AndroidLogcatCommandEntry>();
        public AndroidLogcatCommandEntry[] general = Array.Empty<AndroidLogcatCommandEntry>();
    }

    internal static class AndroidLogcatCommandJsonHelper
    {
        internal static string ToJsonArray<T>(List<T> list)
        {
            var wrapper = new Wrapper<T> { items = list.ToArray() };
            var json = JsonUtility.ToJson(wrapper);
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start)
                return json.Substring(start, end - start + 1);
            return "[]";
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}
