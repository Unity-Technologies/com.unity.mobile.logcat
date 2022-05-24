using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Diagnostics;

public class ForceCrashScript : MonoBehaviour
{
    bool m_CrashOnMainThread;

    private void Start()
    {
        m_CrashOnMainThread = true;
    }

    private void OnGUI()
    {
        var options = new[] { GUILayout.ExpandHeight(true), GUILayout.Width(Screen.width) };
        GUI.skin.button.fontSize = 30;
        GUILayout.BeginVertical(GUILayout.Height(Screen.height));
        if (GUILayout.Button("Thread: " + (m_CrashOnMainThread ? "Main" : "Other"), options))
            m_CrashOnMainThread = !m_CrashOnMainThread;
        foreach (var crashType in (ForcedCrashCategory[])Enum.GetValues(typeof(ForcedCrashCategory)))
        {
            if (GUILayout.Button(crashType.ToString(), options))
            {
                if (m_CrashOnMainThread)
                    Utils.ForceCrash(crashType);
                else
                {
                    var t = new Thread(() => Utils.ForceCrash(crashType));
                    t.Start();
                }
            }
        }

        if (GUILayout.Button("Native Assert", options))
        {
            if (m_CrashOnMainThread)
                Utils.NativeAssert("MyAssert");
            else
            {
                var t = new Thread(() => Utils.NativeAssert("MyAssert"));
                t.Start();
            }
        }

        if (GUILayout.Button("Native Error", options))
        {
            if (m_CrashOnMainThread)
                Utils.NativeError("MyError");
            else
            {
                var t = new Thread(() => Utils.NativeError("MyError"));
                t.Start();
            }
        }
        GUILayout.EndVertical();
    }
}
