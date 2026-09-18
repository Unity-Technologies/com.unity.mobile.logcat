using NUnit.Framework;
using System;
using UnityEngine;
using UnityEditor;
using Unity.Android.Logcat;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;

internal class AndroidLogcatIntegrationTestBase
{
    // How long a device is given to do something before the test gives up. Every
    // wait here is for one adb round trip or one screen change, both of which take
    // a second or two on the devices this has run on.
    protected const float kDefaultTimeout = 10.0f;
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

    /// <summary>
    /// A device that has dozed off composes nothing, so a mirrored display hands over
    /// no frames and `screenrecord` never starts - both of which surface as a test
    /// timing out for reasons that have nothing to do with the code under test. Waking
    /// it is part of putting the device in a known state, and it is cheap enough to do
    /// per test rather than once per fixture.
    /// </summary>
    [SetUp]
    protected void WakeDevice()
    {
        if (m_Device == null)
            return;

        m_Device.WakeUp();
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

    protected IEnumerator WaitForCondition(string name, Func<bool> condition, float timeOutInSeconds = kDefaultTimeout, Func<string> additionalErrorMessage = null)
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
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", message);
    }

    protected static string GetOrCreateArtifactsPath()
    {
        var root = Workspace.GetAritfactsPath();
        Directory.CreateDirectory(root);

        var name = TestContext.CurrentContext.Test.Name;
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    protected IReadOnlyList<string> GetContentsOnDevice(string path)
    {
        var result = Runtime.Tools.ADB.Run(new[] { "shell", "ls", path }, $"Couldn't get contents in '{path}'");
        return result.Split(new[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    protected void AssertFileExistanceOnDevice(string path, bool shouldExist)
    {
        var exists = AndroidLogcatUtilities.FileExists(m_Runtime, Device, path);
        if (shouldExist)
            Assert.IsTrue(exists, $"File {path} should exist");
        else
            Assert.IsFalse(exists, $"File {path} shouldn't exist");
    }

    protected void AssertFileExistanceOnHost(string path, bool shouldExist)
    {
        if (shouldExist)
            Assert.IsTrue(File.Exists(path), $"File {path} should exist");
        else
            Assert.IsFalse(File.Exists(path), $"File {path} shouldn't exist");
    }

    protected void SafeDeleteOnDevice(IAndroidLogcatDevice device, string path)
    {
        try
        {
            m_Runtime.Tools.ADB.Run(new[]
            {
                    $"-s {device.Id}",
                    $"shell rm {path}"
                }, $"Failed to delete {path}");
        }
        catch
        {
            // ignored
        }
    }

    protected void SafeDeleteOnHost(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
