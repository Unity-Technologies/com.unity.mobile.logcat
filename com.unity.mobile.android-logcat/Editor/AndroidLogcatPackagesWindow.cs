using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    // adb shell cmd package list packages -3 -U -i

    internal class AndroidLogcatPackagesWindow : EditorWindow
    {
        MultiColumnListView m_ListView;

        [MenuItem("Test/Test")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            AndroidLogcatPackagesWindow window = (AndroidLogcatPackagesWindow)EditorWindow.GetWindow(typeof(AndroidLogcatPackagesWindow));
            window.Show();
        }

        private void OnEnable()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.mobile.android-logcat/Editor/UI/Layouts/AndroidLogcatPackagesLayout.uxml");
            tree.CloneTree(rootVisualElement);

            m_ListView = rootVisualElement.Q<MultiColumnListView>();

            m_ListView.itemsSource = GetPackageEntries().ToList();
            CreateLabel(nameof(PackageEntry.Name), (e) => e.Name);
            CreateLabel(nameof(PackageEntry.Installer), (e) => e.Installer);
            CreateLabel(nameof(PackageEntry.UID), (e) => e.UID);
        }

        void CreateLabel(string name, Func<PackageEntry, string> getText, Func<PackageEntry, string> getTooltip = null)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () =>
            {
                var label = new PackageEntryLabel();
                label.RegisterCallback<MouseDownEvent, PackageEntryLabel>((e, l) =>
                {
                    switch (e.button)
                    {
                        case 0:
                            if (e.clickCount == 2)
                            {
                                //OnSelectEntryInListView(l.Entry);
                            }
                            break;
                    }
                }, label);
                return label;
            };

            m_ListView.columns[id].bindCell = (element, index) =>
            {
                var label = GetInitializedElement<PackageEntryLabel>(element, index);
                label.text = getText(label.Entry);
                if (getTooltip != null)
                    label.tooltip = getTooltip(label.Entry);
            };
        }

        void CreateButton(string name)
        {
            var id = name.ToLower();
            m_ListView.columns[id].makeCell = () => new PackageEntryButton();
            m_ListView.columns[id].bindCell = (element, index) =>
            {
                var button = GetInitializedElement<PackageEntryButton>(element, index);
                button.text = "Hello";
            };
        }

        T GetInitializedElement<T>(VisualElement element, int index) where T : PackageEntryVisualElement
        {
            var packageEntryElement = (PackageEntryVisualElement)element;
            packageEntryElement.Entry = (PackageEntry)m_ListView.itemsSource[index];
            return (T)packageEntryElement;
        }

        PackageEntry[] GetPackageEntries()
        {
            return AndroidLogcatUtilities.RetrievePackages(
                AndroidLogcatManager.instance.Runtime.Tools.ADB,
                AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice);
        }

        void OnGUI()
        {
           // GetPackageEntries();
        }
    }
}
