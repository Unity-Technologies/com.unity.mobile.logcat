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
        AndroidLogcatCaptureVideo.KillRemoteRecorder(Runtime, Device);
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

        File.Copy(Runtime.CaptureScreenshot.GetImagePath(Device), Path.Combine(GetOrCreateArtifactsPath(), "screenshot.png"), true);
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
