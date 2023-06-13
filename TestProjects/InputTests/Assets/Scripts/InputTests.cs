using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputTests : MonoBehaviour
{
    static KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    List<string> m_Information = new List<string>();
    Dictionary<KeyCode, int> m_Clicks = new Dictionary<KeyCode, int>();
    string m_Text = "";


    void Start()
    {
        foreach (var k in AllKeys)
            m_Clicks[k] = 0;
    }

    void LogInfo(string message)
    {
        m_Information.Add(message);
        if (m_Information.Count > 10)
            m_Information.RemoveAt(0);
    }

    void Update()
    {
        foreach (var k in AllKeys)
        {
            if (Input.GetKeyDown((KeyCode)k))
            {
                m_Clicks[k]++;
                LogInfo($"Key {k} was pressed {m_Clicks[k]} times");
            }
        }
    }

    private static void Setup(float multiplier = 1.0f)
    {
        var uiMultiplier = (Screen.height / 40) * multiplier;
        var s = (int)(uiMultiplier * multiplier);
        GUI.skin.button.fontSize = s;
        GUI.skin.label.fontSize = s;
        GUI.skin.textField.fontSize = s;
        GUI.skin.textArea.fontSize = s;
    }

    private void OnGUI()
    {
        Setup();
        GUILayout.Space(10);
        m_Text = GUILayout.TextArea(m_Text, GUILayout.Width(Screen.width));
        GUILayout.Label("Key presses: ");
        foreach (var s in m_Information)
            GUILayout.Label(s);
    }
}
