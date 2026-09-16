using System;
using NUnit.Framework;
using System.Collections;
using Unity.Android.Logcat;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;

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

        File.Copy(Runtime.CaptureScreenshot.GetLatestImagePath(Device), Path.Combine(GetOrCreateArtifactsPath(), "screenshot.png"), true);
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

        File.Copy(Runtime.CaptureVideo.GetVideoPath(Device), Path.Combine(GetOrCreateArtifactsPath(), "video.mp4"), true);
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

        File.Copy(Runtime.CaptureVideo.GetVideoPath(Device), Path.Combine(GetOrCreateArtifactsPath(), "video.mp4"), true);
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
        File.WriteAllText(Path.Combine(GetOrCreateArtifactsPath(), "errors.txt"), errors);
    }
}
