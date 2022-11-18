using UnityEditor.IMGUI.Controls;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageListItem : TreeViewItem
    {
        internal PackageEntry PackageEntry { get; }

        internal AndroidLogcatPackageListItem(PackageEntry entry)
            : base(entry.GetId(), 0)
        {
            PackageEntry = entry;
        }

        public override bool hasChildren => false;
    }
}
