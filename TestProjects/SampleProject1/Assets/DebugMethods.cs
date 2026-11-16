using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugMethods : MonoBehaviour
{
    const int kMaxTypedLength = 200;

    // What was typed, as Input.inputString reports it: backspace removes the last
    // character and Enter starts a new line.
    string m_Typed = string.Empty;
    GUIStyle m_TypedStyle;
    // Updated every frame, so the screen shows the time whatever the tick is doing.
    float m_Time;

    // Start is called before the first frame update
    void Start()
    {
        TestDebug();
        StartCoroutine(Tick());
    }

    // A steady message to line up against a recording's frames.
    IEnumerator Tick()
    {
        var wait = new WaitForSeconds(3);
        while (true)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Tick: {0:0.00}", Time.time);
            yield return wait;
        }
    }

    void Update()
    {
        m_Time = Time.time;

        var input = Input.inputString;
        if (string.IsNullOrEmpty(input))
            return;

        foreach (var c in input)
        {
            if (c == '\b')
            {
                if (m_Typed.Length > 0)
                    m_Typed = m_Typed.Substring(0, m_Typed.Length - 1);
            }
            else if (c == '\n' || c == '\r')
            {
                m_Typed += '\n';
            }
            else
            {
                m_Typed += c;
            }
        }

        if (m_Typed.Length > kMaxTypedLength)
            m_Typed = m_Typed.Substring(m_Typed.Length - kMaxTypedLength);

        // Logged too, so typing shows up in logcat.
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Typed: '{0}'", Escape(input));
    }

    void OnGUI()
    {
        if (m_TypedStyle == null)
        {
            m_TypedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(24, Screen.height / 30),
                wordWrap = true
            };
            m_TypedStyle.normal.textColor = Color.white;
        }

        var margin = Screen.width * 0.05f;
        var rect = new Rect(margin, margin, Screen.width - 2 * margin, Screen.height - 2 * margin);
        // The time running, to compare against the logcat panel.
        var text = $"Time: {m_Time:0.00}\nTyped: {m_Typed}_";
        GUI.Label(rect, text, m_TypedStyle);
    }

    static string Escape(string input)
    {
        return input.Replace("\b", "\\b").Replace("\n", "\\n").Replace("\r", "\\r");
    }


    public void TestDebug()
    {
        Debug.Log("This is a simple LOG message");
        Debug.Log("This is a simple LOG message, with context", this.gameObject);
        // Debug.Assert(true);
        // Debug.Assert(true, this.gameObject);
        // Debug.Assert(true, "A simple Assert true");
        // Debug.Assert(false);
        // Debug.Assert(false, this.gameObject);
        // Debug.Assert(false, "A simple Assert false");
        Debug.LogAssertion("This is a simple ASSERTION message");
        Debug.LogAssertion("This is a simple ASSERTION message, with context", this.gameObject);
        Debug.LogError("This is a simple ERROR message");
        Debug.LogError("This is a simple ERROR message, with context", this.gameObject);
        Debug.LogException(new UnityException("This is a simple EXCEPTION message"));
        Debug.LogException(new UnityException("This is a simple EXCEPTION message, with context"), this.gameObject);
        Debug.LogWarning("This is a simple WARNING message");
        Debug.LogWarning("This is a simple WARNING message, with context", this.gameObject);
    }
}
