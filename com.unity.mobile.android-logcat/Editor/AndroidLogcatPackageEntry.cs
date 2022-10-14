using System;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class PackageEntry
    {
        public string Name { set; get; }

        internal int GetId() => GetHashCode();
    }
}
