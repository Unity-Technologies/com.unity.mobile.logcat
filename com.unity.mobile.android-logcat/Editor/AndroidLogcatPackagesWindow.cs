using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    // adb shell cmd package list packages -3 -U -i

    internal class AndroidLogcatPackagesWindow : EditorWindow
    {
        AndroidLogcatPackageListView m_View;
        AndroidLogcatPackageListViewState m_State;
        [MenuItem("Window/My Window")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            AndroidLogcatPackagesWindow window = (AndroidLogcatPackagesWindow)EditorWindow.GetWindow(typeof(AndroidLogcatPackagesWindow));
            window.Show();
        }

        private void OnEnable()
        {
            m_State = AndroidLogcatPackageListViewState.CreateOrInitializeTreeState(m_State);
            m_View = new AndroidLogcatPackageListView(m_State, GetPackageEntries());
        }

        PackageEntry[] GetPackageEntries()
        {
            return AndroidLogcatUtilities.RetrievePackages(
                AndroidLogcatManager.instance.Runtime.Tools.ADB,
                AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice);
        }

        void OnGUI()
        {
            if (GUILayout.Button("Reload"))
                m_View.Reload(GetPackageEntries());
            var rc = GUILayoutUtility.GetRect(new GUIContent(""), GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (m_View != null)
                m_View.OnGUI(rc);
            else
                GUILayout.Label("Package View failed to create");

            if (m_View.RequiresUpdating)
                m_View.Reload(GetPackageEntries());
        }
    }
}
