using NUnit.Framework;
using System;
using UnityEngine;
using UnityEditor;
using Unity.Android.Logcat;
using System.Collections;
using System.IO;

internal class AndroidLogcatIntegrationTestBase
{
    protected const float kDefaulTimeOut = 10.0f;
    private AndroidLogcatRuntime m_Runtime;
    private IAndroidLogcatDevice m_Device;
    private int m_Ticks;

    public AndroidLogcatRuntimeBase Runtime => m_Runtime;
    public IAndroidLogcatDevice Device => m_Device;

    public int GetTicks() => m_Ticks;

    /// <summary>
    /// Used to measure Editor ticking. Since Time.frameCount doesn't increase while running editor tests
    /// </summary>
    private void Tick()
    {
        m_Ticks++;
    }

    [OneTimeSetUp]
    protected void InitRuntime()
    {
        EditorApplication.update += Tick;

        if (m_Runtime != null)
            throw new Exception("Runtime was not shutdown by previous test?");
        m_Runtime = new AndroidLogcatRuntime();
        m_Runtime.Initialize();

        var deviceInfo = Workspace.GetAndroidDeviceInfo();
        if (!string.IsNullOrEmpty(deviceInfo))
        {
            Console.WriteLine($"Connecting to Android Device");
            var result = m_Runtime.Tools.ADB.Run(new[]
            {
                "connect",
                deviceInfo
            },
            $"Failed to connect to '{deviceInfo}'");

            Console.WriteLine($"Result:\n{result}");
        }

        m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);

        m_Device = m_Runtime.DeviceQuery.FirstConnectedDevice;

        if (m_Device == null)
            throw new Exception("No Android Device connected?");
    }

    [OneTimeTearDown]
    protected void ShutdownRuntime()
    {
        EditorApplication.update -= Tick;
        if (m_Runtime == null)
            throw new Exception("Runtime was not created?");
        m_Runtime.Shutdown();
        m_Runtime = null;
    }

    protected AndroidLogcat CreateLogcatInstance()
    {
        return new AndroidLogcat(Runtime, Runtime.Tools.ADB, Device, 0, Priority.Verbose, new FilterOptions(), new string[] { });
    }

    protected IEnumerator Waiting()
    {
#if UNITY_EDITOR
        // WaitForEndOfFrame doesn't work in batch mode
        // Time.frameCount doesn't increase when running tests in Editor
        int start = GetTicks();
        return new WaitUntil(() => GetTicks() - start >= 1);

#else
        yield return new WaitForEndOfFrame();
#endif
    }

    protected IEnumerator WaitForCondition(string name, Func<bool> condition, float timeOutInSeconds = kDefaulTimeOut, Func<string> additionalErrorMessage = null)
    {
        m_Runtime.OnUpdate();

        var start = Time.realtimeSinceStartup;

        while (condition() == false)
        {
            if (Time.realtimeSinceStartup - start > timeOutInSeconds)
            {
                var msg = $"TimeOut ({timeOutInSeconds} seconds) while waiting for '{name}'";
                if (additionalErrorMessage != null)
                    msg += Environment.NewLine + additionalErrorMessage.Invoke();
                throw new Exception(msg);
            }
            yield return Waiting();

            m_Runtime.OnUpdate();
        }
    }

    protected static void Log(string message)
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
    }

    protected static string GetOrCreateArtifactsPath()
    {
        var name = TestContext.CurrentContext.Test.Name;
        var root = Workspace.IsRunningOnYamato() ?
            Path.Combine(Application.dataPath, "../../../upm-ci~/test-results/editor-android") :
            Path.Combine(Application.dataPath, "../LocalTestResults");
        Directory.CreateDirectory(root);

        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
