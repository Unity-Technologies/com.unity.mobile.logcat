using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageUtilities
    {
        enum PackageUtilitiesTab
        {
            LaunchOptions,
            Others
        }

        ToolbarToggle m_LaunchOptions;
        ToolbarToggle m_Others;
        VisualElement m_TabContents;
        VisualElement m_TabLaunchOptions;
        VisualElement m_TabOthers;
        ListView m_LaunchableActivities;

        Button m_LaunchApplication;
        Button m_StopApplication;

        IAndroidLogcatDevice m_Device;
        PackageEntry m_PackageEntry;

        internal string SelectedActivity
        {
            get
            {
                var item = m_LaunchableActivities.selectedItem;
                return item?.ToString();
            }
        }

        internal AndroidLogcatPackageUtilities(VisualElement root)
        {
            m_TabContents = root.Q("package-tabs-contents");
            m_LaunchOptions = root.Q<ToolbarToggle>("package-launch-options");
            m_Others = root.Q<ToolbarToggle>("package-others");

            m_LaunchOptions.RegisterValueChangedCallback((v) => SelectTab(PackageUtilitiesTab.LaunchOptions));
            m_Others.RegisterValueChangedCallback((v) => SelectTab(PackageUtilitiesTab.Others));

            m_TabLaunchOptions = root.Q("package-tab-launch-options");
            m_TabOthers = root.Q("package-tab-others");
            m_LaunchableActivities = root.Q<ListView>("launchable-activities");

            m_LaunchApplication = root.Q<Button>("launch-android-application");
            m_LaunchApplication.clicked += () =>
            {
                if (m_Device == null ||
                    m_PackageEntry == null)
                    return;
                m_Device.StartOrResumePackage(m_PackageEntry.Name, SelectedActivity);
            };

            m_StopApplication = root.Q<Button>("stop-android-application");

            var dispatcher = AndroidLogcatManager.instance.Runtime.Dispatcher;
            root.Q<Button>("android-back-button").clicked += () =>
            {
                // TODO:
                AndroidLogcatManager.instance.Runtime.DeviceQuery.FirstConnectedDevice.SendKeyAsync(dispatcher, AndroidKeyCode.BACK);
            };

            root.Q<Button>("android-home-button").clicked += () =>
            {
                AndroidLogcatManager.instance.Runtime.DeviceQuery.FirstConnectedDevice.SendKeyAsync(dispatcher, AndroidKeyCode.HOME);
            };

            root.Q<Button>("android-overview-button").clicked += () =>
            {
                AndroidLogcatManager.instance.Runtime.DeviceQuery.FirstConnectedDevice.SendKeyAsync(dispatcher, AndroidKeyCode.MENU);
            };

            // TODO: take from settings
            SelectTab(PackageUtilitiesTab.LaunchOptions);
        }

        private void SelectTab(PackageUtilitiesTab tab)
        {
            m_LaunchOptions.SetValueWithoutNotify(tab == PackageUtilitiesTab.LaunchOptions);
            m_Others.SetValueWithoutNotify(tab == PackageUtilitiesTab.Others);

            var toRemove = m_TabContents.Children().ToArray();
            foreach (var c in toRemove)
                m_TabContents.Remove(c);

            if (tab == PackageUtilitiesTab.LaunchOptions)
                m_TabContents.Add(m_TabLaunchOptions);
            if (tab == PackageUtilitiesTab.Others)
                m_TabContents.Add(m_TabOthers);
        }

        internal void RefreshActivities(IAndroidLogcatDevice device, PackageEntry packageEntry, List<string> activities)
        {
            m_PackageEntry = packageEntry;
            m_Device = device;
            m_LaunchableActivities.itemsSource = activities;
            if (activities.Count > 0)
                m_LaunchableActivities.selectedIndex = 0;
            m_LaunchableActivities.RefreshItems();
        }
    }
}
