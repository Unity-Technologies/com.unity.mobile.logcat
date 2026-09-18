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
    private string VideoPathOnHost => Runtime.CaptureVideo.GetVideoPath(Device);

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
        // Need to kill screen recorder before attempting to delete files
        AndroidLogcatUtilities.KillScreenRecordProcess(Runtime, Device);
        SafeDeleteOnDevice(Device, AndroidLogcatCaptureVideo.VideoPathOnDevice);
        SafeDeleteOnHost(VideoPathOnHost);
    }

    [UnityTest]
    public IEnumerator CanGetScreenshot()
    {
        var completed = false;
        Runtime.CaptureScreenshot.QueueScreenCapture(Device, () =>
        {
            completed = true;
        });

        yield return WaitForCondition("Waiting for screenshot", () => completed);

        var texture = Runtime.CaptureScreenshot.ImageTexture;
        Assert.IsNotNull(texture, "Expected to have a valid texture");

        Assert.Greater(texture.width, 10);
        Assert.Greater(texture.height, 10);

        CopyToArtifacts("screenshot.png", Runtime.CaptureScreenshot.GetLatestImagePath(Device));
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
            var completed = false;
            Runtime.CaptureScreenshot.QueueScreenCapture(Device, () => completed = true);
            yield return WaitForCondition($"Waiting for screenshot {i + 1}", () => completed);
        }

        var screenshots = Runtime.CaptureScreenshot.GetScreenshots();
        Assert.AreEqual(before + 2, CountScreenshotsOf(prefix),
            "Both captures should have appeared in the list");

        // Grouped by device, ascending by number within each group.
        for (var i = 1; i < screenshots.Count; i++)
        {
            if (screenshots[i - 1].DevicePrefix == screenshots[i].DevicePrefix)
            {
                Assert.Less(screenshots[i - 1].Number, screenshots[i].Number,
                    "Numbering within a device should ascend");
            }
            else
            {
                Assert.Less(string.Compare(screenshots[i - 1].DevicePrefix, screenshots[i].DevicePrefix, StringComparison.Ordinal), 0,
                    "Devices should be grouped together");
            }
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
        var completed = false;
        Runtime.CaptureScreenshot.QueueScreenCapture(Device, () => completed = true);
        yield return WaitForCondition("Waiting for screenshot", () => completed);

        var path = Runtime.CaptureScreenshot.GetLatestImagePath(Device);
        Assert.IsTrue(File.Exists(path));
        var before = Runtime.CaptureScreenshot.GetScreenshots().Count;

        Assert.IsTrue(Runtime.CaptureScreenshot.DeleteScreenshot(path), "Delete should have succeeded");

        Assert.IsFalse(File.Exists(path), "The file should be gone from disk");
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
        var completed = false;
        Runtime.CaptureScreenshot.QueueScreenCapture(Device, () => completed = true);
        yield return WaitForCondition("Waiting for screenshot", () => completed);

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

        var entry = screenshots.First(s => s.Path == renamed);
        Assert.AreEqual(newName, entry.Name);
        Assert.AreEqual(string.Empty, entry.DevicePrefix);
        Assert.AreEqual(0, entry.Number);

        // Renaming onto a name that already exists must refuse rather than overwrite.
        var second = false;
        Runtime.CaptureScreenshot.QueueScreenCapture(Device, () => second = true);
        yield return WaitForCondition("Waiting for a second screenshot", () => second);
        var other = Runtime.CaptureScreenshot.GetLatestImagePath(Device);

        LogAssert.Expect(LogType.Error, new Regex("already exists"));
        Assert.IsFalse(Runtime.CaptureScreenshot.RenameScreenshot(other, newName));
        Assert.IsTrue(File.Exists(other), "The file should be untouched after a refused rename");

        LogAssert.Expect(LogType.Error, new Regex("not a usable file name"));
        Assert.IsFalse(Runtime.CaptureScreenshot.RenameScreenshot(other, "bad/name"));

        // Cleanup, so the next run does not trip over the fixed name.
        Runtime.CaptureScreenshot.DeleteScreenshot(renamed);
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
    public IEnumerator CanGetVideo()
    {
        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(VideoPathOnHost, false);

        var recordingResult = AndroidLogcatCaptureVideo.Result.Failure;
        Runtime.CaptureVideo.StartRecording(Device, (r, s) =>
        {
            recordingResult = r;
        });

        // Starting recording without stoping previous one, should throw
        Assert.Throws(typeof(InvalidOperationException), () => Runtime.CaptureVideo.StartRecording(Device, null));

        yield return WaitForCondition("Waiting for Android's screenrecord to become active",
            () => Runtime.CaptureVideo.IsRemoteRecorderActive(Device));

        var start = DateTime.Now;
        yield return WaitForCondition("Recording video", () => (DateTime.Now - start).TotalSeconds > 5.0f);
        var result = Runtime.CaptureVideo.StopRecording();
        Assert.IsTrue(result, "Failed to stop the recording");
        Assert.AreEqual(AndroidLogcatCaptureVideo.Result.Success, recordingResult);

        result = Runtime.CaptureVideo.StopRecording();
        Assert.IsFalse(result, "StopRecording should return false, since it was already stopped");

        yield return WaitForCondition("Waiting for Android's screenrecord to quit",
            () => !Runtime.CaptureVideo.IsRemoteRecorderActive(Device));

        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(VideoPathOnHost, true);

        CopyToArtifacts("video.mp4", Runtime.CaptureVideo.GetVideoPath(Device));
    }

    [UnityTest]
    public IEnumerator CanGetVideoWithTimeLimit()
    {
        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(VideoPathOnHost, false);

        var recordingTime = 5;
        var recordingResult = AndroidLogcatCaptureVideo.Result.Failure;
        Runtime.CaptureVideo.StartRecording(Device, (r, s) =>
        {
            recordingResult = r;
        }, TimeSpan.FromSeconds(recordingTime));

        yield return WaitForCondition($"Waiting for the recording to stop automatically (Should stop in {recordingTime} seconds)",
            () => recordingResult == AndroidLogcatCaptureVideo.Result.Success, 20);

        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(VideoPathOnHost, true);

        CopyToArtifacts("video.mp4", Runtime.CaptureVideo.GetVideoPath(Device));
    }

    [UnityTest]
    public IEnumerator CaptureVideoHandlesErrors()
    {
        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(VideoPathOnHost, false);

        var recordingResult = AndroidLogcatCaptureVideo.Result.Success;
        Runtime.CaptureVideo.StartRecording(Device, (r, p) =>
        {
            recordingResult = r;
        }, TimeSpan.FromSeconds(180), 0, 0);

        yield return WaitForCondition($"Waiting for the recording to fail",
            () => recordingResult == AndroidLogcatCaptureVideo.Result.Failure, 20);
        var errors = Runtime.CaptureVideo.Errors;
        Assert.Greater(errors.Length, 0);
        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(VideoPathOnHost, false);

        Debug.Log(errors);
        ReportArtifact("errors.txt", errors);
    }
}
