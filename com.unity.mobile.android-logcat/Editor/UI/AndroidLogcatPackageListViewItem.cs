using UnityEditor.IMGUI.Controls;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageListItem : TreeViewItem
    {
        internal PackageEntry PackageEntry { get; }

        internal AndroidLogcatPackageListItem(int depth, PackageEntry entry)
            : base(entry.GetId(), depth)
        {
            PackageEntry = entry;
        }

        public override bool hasChildren => false;
    }
}
