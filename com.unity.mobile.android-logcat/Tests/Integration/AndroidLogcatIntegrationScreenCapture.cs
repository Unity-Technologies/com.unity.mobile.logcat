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
    [OneTimeSetUp]
    protected void Init()
    {
        SafeDeleteOnDevice(Device, AndroidLogcatCaptureVideo.VideoPathOnDevice);
        SafeDeleteOnHost(AndroidLogcatCaptureVideo.VideoPathOnHost);
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

        File.Copy(Runtime.CaptureScreenshot.ImagePath, Path.Combine(GetOrCreateArtifactsPath(), "screenshot.png"), true);
    }

    [UnityTest]
    public IEnumerator CanGetVideo()
    {
        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(AndroidLogcatCaptureVideo.VideoPathOnHost, false);

        Runtime.CaptureVideo.StartRecording(Device);

        // Starting recording without stoping previous one, should throw
        Assert.Throws(typeof(Exception), () => Runtime.CaptureVideo.StartRecording(Device));

        yield return WaitForCondition("Waiting for Android's screenrecord to become active",
            () => Runtime.CaptureVideo.IsRemoteRecorderActive(Device));

        var start = DateTime.Now;
        yield return WaitForCondition("Recording video", () => (DateTime.Now - start).TotalSeconds > 5.0f);
        var result = Runtime.CaptureVideo.StopRecording();
        Assert.IsTrue(result, "Failed to stop the recording");

        result = Runtime.CaptureVideo.StopRecording();
        Assert.IsFalse(result, "StopRecording should return false, since it was already stopped");

        yield return WaitForCondition("Waiting for Android's screenrecord to quit",
            () => !Runtime.CaptureVideo.IsRemoteRecorderActive(Device));

        AssertFileExistanceOnDevice(AndroidLogcatCaptureVideo.VideoPathOnDevice, false);
        AssertFileExistanceOnHost(AndroidLogcatCaptureVideo.VideoPathOnHost, true);

        File.Copy(Runtime.CaptureVideo.VideoPath, Path.Combine(GetOrCreateArtifactsPath(), "video.mp4"), true);

    }
}
