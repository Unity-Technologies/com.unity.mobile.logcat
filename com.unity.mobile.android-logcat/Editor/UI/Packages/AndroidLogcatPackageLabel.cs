using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    interface PackageEntryVisualElement
    {
        PackageEntry Entry { set; get; }
    }

    class PackageEntryLabel : Label, PackageEntryVisualElement
    {
        public PackageEntry Entry { set; get; }
    }

    class PackageEntryButton : Button, PackageEntryVisualElement
    {
        public PackageEntry Entry { set; get; }
    }
}
