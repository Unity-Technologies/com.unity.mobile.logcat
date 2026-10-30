using System;
using NUnit.Framework;
using System.Collections;
using Unity.Android.Logcat;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

[TestFixture]
[RequiresAndroidDevice]
internal class AndroidLogcatRuntimeIntegrationScreenCapture : AndroidLogcatIntegrationTestBase
{

    // These tests take captures and clear the folder between them, so they get one of
    // their own rather than whatever the machine running them has configured. It sits
    // with the rest of the test output - LocalTestResults, or the artifacts path on a
    // build agent - which is where anyone looking at a failure is already looking.
    private string m_CapturesFolder;

    [OneTimeSetUp]
    protected void RedirectCaptures()
    {
        m_CapturesFolder = Runtime.Settings.CaptureOutputDirectory;
        Runtime.Settings.CaptureOutputDirectory =
            Path.Combine(Workspace.GetAritfactsPath(), "Captures").Replace("\\", "/");
        Runtime.CaptureScreenshot.InvalidateScreenshots();
        Log($"Captures go to '{AndroidLogcatUtilities.GetCapturesDirectory(Runtime.Settings)}'");
    }

    [OneTimeTearDown]
    protected void RestoreCaptures()
    {
        // Resolved before the setting goes back, and removed whole: what is in there
        // was all written by these tests.
        var folder = AndroidLogcatUtilities.GetCapturesDirectory(Runtime.Settings);

        Runtime.Settings.CaptureOutputDirectory = m_CapturesFolder;
        Runtime.CaptureScreenshot.InvalidateScreenshots();

        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        catch (Exception ex)
        {
            Log($"Failed to remove '{folder}': {ex.Message}");
        }
    }

    [SetUp]
    protected void Init()
    {
        Cleanup();
    }

    [TearDown]
    protected void Deinit()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        Runtime.CaptureVideo.StopRecording();
        Runtime.LiveStream.StopStreaming();

        // Start from an empty folder - the fixture's own, so this takes nothing with
        // it that these tests did not write. Leftovers from an earlier run are still
        // listed, and they carry another device's prefix or a name a test reuses.
        // Recordings live in the same folder and are listed with them.
        Runtime.CaptureScreenshot.InvalidateScreenshots();
        foreach (var screenshot in Runtime.CaptureScreenshot.GetScreenshots().ToArray())
            Runtime.CaptureScreenshot.DeleteScreenshot(screenshot.Path);
    }

    /// <summary>
    /// Takes a screenshot and waits for it to land, which is where most of these tests
    /// start. Returns the wait rather than yielding it, so the capture is queued as
    /// soon as this is called.
    /// </summary>
    private IEnumerator CaptureScreenshot(string what = "Waiting for screenshot")
    {
        var completed = false;
        Runtime.CaptureScreenshot.QueueScreenCapture(Device, () => completed = true);
        return WaitForCondition(what, () => completed);
    }

    [UnityTest]
    public IEnumerator CanGetScreenshot()
    {
        yield return CaptureScreenshot();

        var texture = Runtime.CaptureScreenshot.ImageTexture;
        Assert.IsNotNull(texture, "Expected to have a valid texture");

        Assert.Greater(texture.width, 10);
        Assert.Greater(texture.height, 10);

        var path = Runtime.CaptureScreenshot.GetLatestImagePath(Device);
        CopyToArtifacts("screenshot.png", path);

        var info = AndroidLogcatScreenshotInfo.Load(path);
        Assert.IsNotNull(info, "Expected details to be saved beside the screenshot");
        Assert.AreEqual(Device.Id, info.deviceId);
        Assert.AreEqual(Device.APILevel, info.apiLevel);
        Assert.Greater(info.displayWidth, 0, $"Expected a display size, got {info.displayWidth}x{info.displayHeight}");
        CopyToArtifacts("screenshot.json", AndroidLogcatScreenshotInfo.PathFor(path));
    }

    /// <summary>
    /// The screenshot list view depends on three things this checks: that a capture
    /// shows up in the list at all (the cache has to be dropped, or the list never
    /// grows), that the list is ordered by number, and that the newest capture becomes
    /// the selected one.
    /// </summary>
    [UnityTest]
    public IEnumerator ScreenshotsAreListedInOrder()
    {
        var prefix = AndroidLogcatUtilities.SanitizeFileName(Device.Id);
        var before = CountScreenshotsOf(prefix);

        for (var i = 0; i < 2; i++)
        {
            yield return CaptureScreenshot($"Waiting for screenshot {i + 1}");
        }

        var screenshots = Runtime.CaptureScreenshot.GetScreenshots();
        Assert.AreEqual(before + 2, CountScreenshotsOf(prefix),
            "Both captures should have appeared in the list");

        // The whole order: files that still carry a device prefix first, grouped by
        // device and ascending by number, then renamed ones by name. A renamed file
        // has neither a prefix nor a number, so it takes part in neither comparison.
        for (var i = 1; i < screenshots.Count; i++)
        {
            var previous = screenshots[i - 1];
            var current = screenshots[i];
            var previousRenamed = string.IsNullOrEmpty(previous.DevicePrefix);
            var currentRenamed = string.IsNullOrEmpty(current.DevicePrefix);
            var pair = $"'{previous.Name}' before '{current.Name}'";

            if (previousRenamed != currentRenamed)
                Assert.IsTrue(currentRenamed, $"Renamed screenshots should come last, found {pair}");
            else if (previousRenamed)
                Assert.Less(string.Compare(previous.Name, current.Name, StringComparison.Ordinal), 0,
                    $"Renamed screenshots should be in name order, found {pair}");
            else if (previous.DevicePrefix == current.DevicePrefix)
                Assert.Less(previous.Number, current.Number,
                    $"Numbering within a device should ascend, found {pair}");
            else
                Assert.Less(string.Compare(previous.DevicePrefix, current.DevicePrefix, StringComparison.Ordinal), 0,
                    $"Devices should be grouped together, found {pair}");
        }

        foreach (var screenshot in screenshots)
        {
            Assert.IsTrue(File.Exists(screenshot.Path), $"{screenshot.Path} should exist");
            Assert.AreEqual(Path.GetFileNameWithoutExtension(screenshot.Path), screenshot.Name,
                "The list label should be the file name without its extension");
        }

        var latest = Runtime.CaptureScreenshot.GetLatestImagePath(Device);
        Assert.AreEqual(latest, Runtime.CaptureScreenshot.SelectedImagePath,
            "The newest capture should be the selected one");
    }

    [UnityTest]
    public IEnumerator CanDeleteScreenshot()
    {
        yield return CaptureScreenshot();

        var path = Runtime.CaptureScreenshot.GetLatestImagePath(Device);
        Assert.IsTrue(File.Exists(path));
        var before = Runtime.CaptureScreenshot.GetScreenshots().Count;

        Assert.IsTrue(Runtime.CaptureScreenshot.DeleteScreenshot(path), "Delete should have succeeded");

        Assert.IsFalse(File.Exists(path), "The file should be gone from disk");
        Assert.IsNull(AndroidLogcatScreenshotInfo.Load(path), "Its details should go with it");
        Assert.AreEqual(before - 1, Runtime.CaptureScreenshot.GetScreenshots().Count,
            "The list should have lost the row");
        // It was the displayed one, so the image is cleared rather than left pointing at
        // a file that no longer exists.
        Assert.AreEqual(string.Empty, Runtime.CaptureScreenshot.SelectedImagePath);
        Assert.IsNull(Runtime.CaptureScreenshot.ImageTexture);

        // Deleting the same path again is not an error, it is just already gone.
        Assert.IsTrue(Runtime.CaptureScreenshot.DeleteScreenshot(path));
    }

    /// <summary>
    /// A renamed screenshot no longer matches &lt;device&gt;_&lt;number&gt;, so this also
    /// covers the scan listing files that do not match the pattern - without that, a
    /// rename would make the file disappear from the list.
    /// </summary>
    [UnityTest]
    public IEnumerator CanRenameScreenshot()
    {
        yield return CaptureScreenshot();

        var path = Runtime.CaptureScreenshot.GetLatestImagePath(Device);
        var prefix = AndroidLogcatUtilities.SanitizeFileName(Device.Id);
        var countBefore = Runtime.CaptureScreenshot.GetScreenshots().Count;
        var ofDeviceBefore = CountScreenshotsOf(prefix);
        var newName = "renamed-by-test";

        Assert.IsTrue(Runtime.CaptureScreenshot.RenameScreenshot(path, newName), "Rename should have succeeded");

        var renamed = Path.Combine(Path.GetDirectoryName(path), newName + ".png").Replace("\\", "/");
        Assert.IsFalse(File.Exists(path), "The old name should be gone");
        Assert.IsTrue(File.Exists(renamed), "The new name should exist");

        // Still listed, still the displayed image, but no longer attributed to a device.
        var screenshots = Runtime.CaptureScreenshot.GetScreenshots();
        Assert.AreEqual(countBefore, screenshots.Count, "The list should still hold it");
        Assert.AreEqual(ofDeviceBefore - 1, CountScreenshotsOf(prefix),
            "A renamed file no longer counts towards its device");
        Assert.AreEqual(renamed, Runtime.CaptureScreenshot.SelectedImagePath,
            "The displayed image should follow the rename");

        Assert.IsNull(AndroidLogcatScreenshotInfo.Load(path), "The details should not be left behind");
        Assert.IsNotNull(AndroidLogcatScreenshotInfo.Load(renamed), "The details should follow the image");

        var entry = screenshots.First(s => s.Path == renamed);
        Assert.AreEqual(newName, entry.Name);
        Assert.AreEqual(string.Empty, entry.DevicePrefix);
        Assert.AreEqual(0, entry.Number);

        // Renaming onto a name that already exists must refuse rather than overwrite.
        yield return CaptureScreenshot("Waiting for a second screenshot");
        var other = Runtime.CaptureScreenshot.GetLatestImagePath(Device);

        LogAssert.Expect(LogType.Error, new Regex("already exists"));
        Assert.IsFalse(Runtime.CaptureScreenshot.RenameScreenshot(other, newName));
        Assert.IsTrue(File.Exists(other), "The file should be untouched after a refused rename");

        LogAssert.Expect(LogType.Error, new Regex("not a usable file name"));
        Assert.IsFalse(Runtime.CaptureScreenshot.RenameScreenshot(other, "bad/name"));
    }

    private int CountScreenshotsOf(string devicePrefix)
    {
        var count = 0;
        foreach (var screenshot in Runtime.CaptureScreenshot.GetScreenshots())
        {
            if (screenshot.DevicePrefix == devicePrefix)
                count++;
        }
        return count;
    }

    [UnityTest]
    public IEnumerator CanRecordTheLiveStream()
    {
        Runtime.LiveStream.StartStreaming(Device, null);
        yield return WaitForCondition("Waiting for the first frame", () => Runtime.LiveStream.FramesReceived > 0);

        // Where the recording landed is known when it stops: it is numbered like a
        // screenshot, and the number is taken when the recording starts.
        var recordingResult = AndroidLogcatCaptureVideo.Result.Failure;
        string recordedPath = null;
        Runtime.CaptureVideo.StartRecording(Device, (r, p) =>
        {
            recordingResult = r;
            recordedPath = p;
        });

        // Starting recording without stoping previous one, should throw
        Assert.Throws(typeof(InvalidOperationException), () => Runtime.CaptureVideo.StartRecording(Device, null));

        // Frames only arrive when the screen changes.
        SendKeyEvent("KEYCODE_APP_SWITCH");
        SendKeyEvent("KEYCODE_HOME");
        yield return WaitFor(3.0, "Recording video");

        var result = Runtime.CaptureVideo.StopRecording();
        Assert.IsTrue(result, $"Failed to stop the recording: {Runtime.CaptureVideo.Errors}");
        Assert.AreEqual(AndroidLogcatCaptureVideo.Result.Success, recordingResult);

        result = Runtime.CaptureVideo.StopRecording();
        Assert.IsFalse(result, "StopRecording should return false, since it was already stopped");

        AssertFileExistanceOnHost(recordedPath, true);
        StringAssert.EndsWith(".mp4", recordedPath, "A recording is saved as an .mp4 capture");

        CopyToArtifacts("video.mp4", recordedPath);
    }

    [Test]
    public void RecordingNeedsTheLiveStream()
    {
        Assert.IsFalse(Runtime.LiveStream.IsStreaming);
        Assert.Throws(typeof(InvalidOperationException), () => Runtime.CaptureVideo.StartRecording(Device, null));
        Assert.IsFalse(Runtime.CaptureVideo.IsRecording);
    }

    private void SendKeyEvent(string keyCode)
    {
        Runtime.Tools.ADB.Run(new[]
        {
            $"-s {Device.Id}",
            "shell",
            "input",
            "keyevent",
            keyCode
        }, $"Failed to send {keyCode} to the device");
    }
}
