using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    interface PackageEntryVisualElement
    {
        PackageEntry Entry { set; get; }
        int Index { set; get; }
    }

    class PackageEntryLabel : Label, PackageEntryVisualElement
    {
        public PackageEntry Entry { set; get; }
        public int Index { set; get; }
    }

    class PackageEntryButton : Button, PackageEntryVisualElement
    {
        public PackageEntry Entry { set; get; }

        public int Index { set; get; }
    }

    class PackagePropertyLabel : Label
    {
        public int Index { set; get; }
    }

}
