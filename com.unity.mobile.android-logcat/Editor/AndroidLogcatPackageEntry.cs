using System;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class PackageEntry
    {
        public string Name { set; get; }
        public string Installer { set; get; }
        public string UID { set; get; }

        internal int GetId() => GetHashCode();
    }
}
