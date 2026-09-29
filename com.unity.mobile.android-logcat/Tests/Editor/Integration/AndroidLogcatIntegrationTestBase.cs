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
    protected const float kDefaultTimeout = 30.0f;
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
    protected void PrepareDevice()
    {
        if (m_Device == null)
            return;

        m_Device.WakeUp();

        // Waking only lights the screen up, and a device left on its lock screen
        // ignores Home and Overview entirely - so a test that expects the screen to
        // change sees nothing move. Devices here have no secure lock, where this
        // dismisses the keyguard outright.
        RunAdb("Failed to dismiss the lock screen", "shell", "wm", "dismiss-keyguard");

        // Cleared so that what the teardown collects is this test's log and not the
        // one before it.
        RunAdb("Failed to clear the device log", "logcat", "-c");
    }

    /// <summary>
    /// What the device had to say and what it looked like when the test ended, kept as
    /// artifacts. A failing live stream test says little by itself - the server writes
    /// what went wrong on the device side to logcat, and the screen shows what the
    /// device was actually doing. Both are gone by the time anyone looks.
    /// <para>
    /// Nothing in here throws or logs an error: a teardown that fails would bury
    /// whatever the test was failing on.
    /// </para>
    /// </summary>
    [TearDown]
    protected void CollectDeviceState()
    {
        if (m_Device == null)
            return;

        var name = AndroidLogcatUtilities.SanitizeFileName(TestContext.CurrentContext.Test.Name);

        try
        {
            CollectLogcat(name);
            CollectScreenshot(name);
        }
        catch (Exception ex)
        {
            Log($"Failed to collect the device state: {ex}");
        }
    }

    private void CollectLogcat(string name)
    {
        var log = RunAdb("Failed to read the device log", "logcat", "-d");
        if (!string.IsNullOrEmpty(log))
            ReportArtifact($"{name}-logcat.txt", log);
    }

    /// <summary>
    /// Captured here rather than with <see cref="AndroidLogcatUtilities.CaptureScreen"/>,
    /// which reports its failures with <c>Debug.LogError</c> - and an unexpected error
    /// log fails the test that is being torn down.
    /// </summary>
    private void CollectScreenshot(string name)
    {
        const string onDevice = "/sdcard/unity-logcat-test-screen.png";
        // A path nothing has written, so the file being there afterwards means this
        // capture worked rather than an earlier one having left something behind.
        var path = ArtifactPath($"{name}-screen.png");

        var capture = RunAdb("Failed to capture the screen", "shell", $"screencap -p {onDevice}");
        var pull = RunAdb("Failed to pull the screenshot", "pull", onDevice, path);
        SafeDeleteOnDevice(m_Device, onDevice);

        if (File.Exists(path))
            return;

        ReportArtifact("failed_to_capture_screenshot.txt",
            $"{name}{Environment.NewLine}{capture}{Environment.NewLine}{pull}");
    }

    /// <summary>
    /// Runs adb against the device under test. Never throws: this is housekeeping
    /// around a test, and a device that will not answer should not be reported as the
    /// test failing.
    /// </summary>
    private string RunAdb(string failureMessage, params string[] args)
    {
        try
        {
            return m_Runtime.Tools.ADB.Run(new[] { $"-s {m_Device.Id}" }.Concat(args).ToArray(), failureMessage);
        }
        catch (Exception ex)
        {
            Log($"{failureMessage}: {ex.Message}");
            return null;
        }
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

    /// <summary>
    /// Waits out a stretch of time, for what cannot be watched for directly: a screen
    /// settling after an app opens, a device falling asleep, a recording running long
    /// enough to be worth stopping.
    /// </summary>
    protected IEnumerator WaitFor(double seconds, string what)
    {
        var start = DateTime.Now;
        return WaitForCondition(what, () => (DateTime.Now - start).TotalSeconds > seconds);
    }

    /// <summary>
    /// Saves something into this test's artifacts folder, which Yamato collects and a
    /// local run leaves behind to look at. A frame count only says the screen changed;
    /// the picture says what it changed to.
    /// </summary>
    protected static void ReportArtifact(string fileName, Texture2D texture)
    {
        ReportArtifact(fileName, texture.EncodeToPNG());
    }

    protected static void ReportArtifact(string fileName, byte[] contents)
    {
        File.WriteAllBytes(ArtifactPath(fileName), contents);
    }

    protected static void ReportArtifact(string fileName, string contents)
    {
        File.WriteAllText(ArtifactPath(fileName), contents);
    }

    /// <summary>
    /// A path in the artifacts folder that nothing has written yet, numbered from the
    /// first one: "frame_0.png", then "frame_1.png" and so on. A test that runs more
    /// than once into the same folder - a rerun, or two editor versions - keeps every
    /// attempt rather than the last one.
    /// </summary>
    protected static string ArtifactPath(string fileName)
    {
        var directory = GetOrCreateArtifactsPath();
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var i = 0; ; i++)
        {
            var path = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(path))
                return path;
        }
    }

    /// <summary>
    /// The same, for something already written to disk - a screenshot or a recording
    /// the code under test produced.
    /// </summary>
    protected static void CopyToArtifacts(string fileName, string sourcePath)
    {
        File.Copy(sourcePath, ArtifactPath(fileName));
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
