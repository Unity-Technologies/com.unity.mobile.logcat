using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class InputTests : MonoBehaviour
{

    List<string> m_Information = new List<string>();
#if ENABLE_INPUT_SYSTEM
    static Key[] AllKeys = (Key[])Enum.GetValues(typeof(Key));
    Dictionary<Key, int> m_Clicks = new Dictionary<Key, int>();
#else
    static KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    Dictionary<KeyCode, int> m_Clicks = new Dictionary<KeyCode, int>();
#endif
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
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Keyboard.current.allControls.ToList().ForEach(c =>
            {
                var keyControl = c as KeyControl;
                if (keyControl == null)
                    return;
                if (keyControl.wasPressedThisFrame)
                {
                    m_Clicks[keyControl.keyCode]++;
                    LogInfo($"Key {keyControl.keyCode} was pressed {m_Clicks[keyControl.keyCode]} times");
                }
            });
        }
#else
        foreach (var k in AllKeys)
        {
            if (Input.GetKeyDown((KeyCode)k))
            {
                m_Clicks[k]++;
                LogInfo($"Key {k} was pressed {m_Clicks[k]} times");
            }
        }
#endif
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
#if ENABLE_INPUT_SYSTEM
        var inputSystem = "New Input System";
#else
        var inputSystem = "Old Input System";
#endif
        GUILayout.Label($"Key presses ({inputSystem}): ");
        foreach (var s in m_Information)
            GUILayout.Label(s);
    }
}
