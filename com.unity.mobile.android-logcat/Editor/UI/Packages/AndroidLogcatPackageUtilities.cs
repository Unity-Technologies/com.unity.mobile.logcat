using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor;
using System.Text;
using UnityEditor.UIElements;
using UnityEngine;

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
        VisualElement m_TabLaunchOptions;
        VisualElement m_TabOthers;

        internal AndroidLogcatPackageUtilities(VisualElement root)
        {
            m_LaunchOptions = root.Q<ToolbarToggle>("package-launch-options");
            m_Others = root.Q<ToolbarToggle>("package-others");

            m_LaunchOptions.RegisterValueChangedCallback((v) => SelectTab(PackageUtilitiesTab.LaunchOptions));
            m_Others.RegisterValueChangedCallback((v) => SelectTab(PackageUtilitiesTab.Others));

            m_TabLaunchOptions = root.Q("package-tab-launch-options");
            m_TabOthers = root.Q("package-tab-others");

            // TODO: take from settings
            SelectTab(PackageUtilitiesTab.LaunchOptions);
        }

        private void SelectTab(PackageUtilitiesTab tab)
        {
            m_LaunchOptions.SetValueWithoutNotify(tab == PackageUtilitiesTab.LaunchOptions);
            m_Others.SetValueWithoutNotify(tab == PackageUtilitiesTab.Others);
            m_TabLaunchOptions.visible = tab == PackageUtilitiesTab.LaunchOptions;
            m_TabOthers.visible = tab == PackageUtilitiesTab.Others;
        }
    }
}
