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
        VisualElement m_TabContents;
        VisualElement m_TabLaunchOptions;
        VisualElement m_TabOthers;

        internal AndroidLogcatPackageUtilities(VisualElement root)
        {
            m_TabContents = root.Q("package-tabs-contents");
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

            var toRemove = m_TabContents.Children().ToArray();
            foreach (var c in toRemove)
                m_TabContents.Remove(c);

            if (tab == PackageUtilitiesTab.LaunchOptions)
                m_TabContents.Add(m_TabLaunchOptions);
            if (tab == PackageUtilitiesTab.Others)
                m_TabContents.Add(m_TabOthers);
            // m_TabLaunchOptions.SetEnabled();
            //m_TabOthers.SetEnabled(tab == PackageUtilitiesTab.Others);
        }
    }
}
