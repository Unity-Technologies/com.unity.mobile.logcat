using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.MPE;
using JetBrains.Annotations;

#if PLATFORM_ANDROID
using UnityEditor.Android;
#endif

// Issues:
// HostView is internal
// ContainerWindow is internal

namespace Unity.Android.Logcat
{
    internal static class ConsoleRoleProvider
    {
        [UnityEditor.MPE.RoleProvider("androidlogcat", UnityEditor.MPE.ProcessEvent.Initialize)]
        public static void InitializeConsoleProcess()
        {
            var consoleWindow = ScriptableObject.CreateInstance<AndroidLogcatConsoleWindow>();
            consoleWindow.Show();
            var view = ScriptableObject.CreateInstance<HostView>();
            view.SetActualViewInternal(consoleWindow, true);
            ContainerWindow cw = ScriptableObject.CreateInstance<ContainerWindow>();
            cw.m_DontSaveToLayout = false;
            cw.rootView = view;
            cw.Show(ShowMode.MainWindow, false, true, true);

        }
        [MenuItem("UMPE/Logcat")]
        public static void LaunchLogcatOutOfProcess()
        {
            var args = new List<string>();
            args.Add("ump-window-title");
            args.Add("Logcat");
            ProcessService.LaunchSlave("androidlogcat", args.ToArray());
            //StartEditorModeProcess("androidlogcat", "Test", "androidlogcat");
        }

        internal static void StartEditorModeProcess(string roleName, string windowTitle, string editorMode, params string[] others)
        {
            var args = new List<string>();
            args.Add("ump-window-title");
            args.Add(windowTitle);
            args.Add("ump-cap");
            args.Add("main_window");
            args.Add("ump-cap");
            args.Add("menu_bar");
            //args.Add("editor-mode");
            //args.Add(editorMode);

            foreach (var other in others)
            {
                args.Add(other);
            }

            ProcessService.LaunchSlave(roleName, args.ToArray());
        }
    }
}



internal class AutomotiveWindow : EditorWindow
{
    private string m_Text;
    private Vector2 m_ScrollPosition;

    [UsedImplicitly]
    internal void OnEnable()
    {
        titleContent.text = "Automotive";
        m_Text = System.IO.File.ReadAllText("Packages/com.unity.automotive/automotive.mode");
    }

    [UsedImplicitly]
    internal void OnGUI()
    {
        using (var _1 = new GUILayout.ScrollViewScope(m_ScrollPosition))
        {
            m_Text = GUILayout.TextArea(m_Text);
            m_ScrollPosition = _1.scrollPosition;
        }
    }

    [UsedImplicitly, CommandHandler("Automotive/Reload")]
    private static void Reload(CommandExecuteContext ctx)
    {
        CommandService.Execute("ModeService/Refresh");
        ModeService.ChangeModeById("default");
        ModeService.ChangeModeById("com.unity.automotive");
    }

    [UsedImplicitly, CommandHandler("Automotive/Quit")]
    private static void Quit(CommandExecuteContext ctx)
    {
        ModeService.ChangeModeById("default");
    }
}
