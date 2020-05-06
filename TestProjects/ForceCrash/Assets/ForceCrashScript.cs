using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Diagnostics;

public class ForceCrashScript : MonoBehaviour
{
    private void OnGUI()
    {
        GUILayout.BeginVertical(GUILayout.Height(Screen.height));
        foreach (var crashType in (ForcedCrashCategory[])Enum.GetValues(typeof(ForcedCrashCategory)))
        {
            if (GUILayout.Button(crashType.ToString(), new[] { GUILayout.ExpandHeight(true), GUILayout.Width(Screen.width) }))
            {
                Utils.ForceCrash(crashType);
            }
        }
        GUILayout.EndVertical();
    }
}
