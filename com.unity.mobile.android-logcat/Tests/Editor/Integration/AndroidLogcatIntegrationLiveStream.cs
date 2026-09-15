using System;
using NUnit.Framework;
using System.Collections;
using Unity.Android.Logcat;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;

[TestFixture]
[RequiresAndroidDevice]
internal class AndroidLogcatRuntimeIntegrationLiveStream : AndroidLogcatIntegrationTestBase
{
    // Small and slow on purpose: these tests care that frames arrive and are
    // decodable, not about throughput, and a smaller stream starts sooner.
    const int kMaxSize = 512;
    const int kMaxFps = 15;

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
        // Leaving a stream running would hold a mirrored display on the device and
        // make the next test fail with "Already streaming".
        Runtime.LiveStream.StopStreaming();
    }

    [UnityTest]
    public IEnumerator CanStreamDeviceScreen()
    {
        var result = AndroidLogcatLiveStream.Result.Failure;
        var stopped = false;

        Runtime.LiveStream.StartStreaming(Device, r =>
        {
            result = r;
            stopped = true;
        }, maxSize: kMaxSize, maxFps: kMaxFps);

        Assert.IsTrue(Runtime.LiveStream.IsStreaming, "Expected to be streaming right after starting");

        // Starting a second stream without stopping the first should throw
        Assert.Throws(typeof(InvalidOperationException), () => Runtime.LiveStream.StartStreaming(Device, null));

        // Pushing the jar, starting the server and connecting all happen before the
        // first frame, so this gets a longer timeout than the rest.
        yield return WaitForCondition("Waiting for the first frame",
            () => Runtime.LiveStream.Texture != null,
            30,
            () => Runtime.LiveStream.Errors);

        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors, "Did not expect any errors while streaming");

        var texture = Runtime.LiveStream.Texture;
        Assert.IsNotNull(texture, "Expected to have a valid texture");
        Assert.Greater(texture.width, 10);
        Assert.Greater(texture.height, 10);
        // max_size caps the longest side, so neither side may exceed it.
        Assert.LessOrEqual(Math.Max(texture.width, texture.height), kMaxSize,
            $"Expected the longest side to be capped at {kMaxSize}");

        // Written out so the frame can be eyeballed: a channel-order or row-order
        // mistake still produces a texture of the right size.
        File.WriteAllBytes(Path.Combine(GetOrCreateArtifactsPath(), "frame.png"), texture.EncodeToPNG());

        // Note there is deliberately no assertion about a frame rate here. A mirrored
        // display only produces a buffer when the screen changes, so a device sitting
        // on a static screen legitimately sends nothing at all - see
        // StreamsFramesWhileScreenChanges. What matters here is that the stream stays
        // up rather than dying once the first frame is through.
        var start = DateTime.Now;
        yield return WaitForCondition("Letting the stream run", () => (DateTime.Now - start).TotalSeconds > 2.0);

        Assert.IsTrue(Runtime.LiveStream.IsStreaming, "Expected the stream to still be running");
        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors, "Did not expect errors while streaming");

        Log($"Received {Runtime.LiveStream.FramesReceived} frames at {texture.width}x{texture.height}");

        Assert.IsTrue(Runtime.LiveStream.StopStreaming(), "Failed to stop the stream");
        Assert.IsFalse(Runtime.LiveStream.IsStreaming, "Expected to have stopped streaming");
        Assert.IsTrue(stopped, "Expected the stop callback to have been invoked");
        Assert.AreEqual(AndroidLogcatLiveStream.Result.Success, result);

        Assert.IsFalse(Runtime.LiveStream.StopStreaming(),
            "StopStreaming should return false, since it was already stopped");
    }

    /// <summary>
    /// A mirrored display hands over a buffer only when composition changes, so frame
    /// delivery is driven by the screen rather than by a clock: an idle device sends
    /// roughly nothing (measured: 1 frame in 5 seconds), and a screen that is animating
    /// saturates the max_fps cap. This makes the screen change and checks that frames
    /// follow.
    /// </summary>
    [UnityTest]
    public IEnumerator StreamsFramesWhileScreenChanges()
    {
        Runtime.LiveStream.StartStreaming(Device, null, maxSize: kMaxSize, maxFps: kMaxFps);

        yield return WaitForCondition("Waiting for the first frame",
            () => Runtime.LiveStream.FramesReceived > 0,
            30,
            () => Runtime.LiveStream.Errors);

        var framesBefore = Runtime.LiveStream.FramesReceived;

        // Each of these animates for a few hundred milliseconds. They are sent before
        // waiting rather than interleaved because the reader thread counts frames on
        // its own, so the count has already moved by the time we look.
        SendKeyEvent("KEYCODE_APP_SWITCH");
        SendKeyEvent("KEYCODE_HOME");
        SendKeyEvent("KEYCODE_APP_SWITCH");
        SendKeyEvent("KEYCODE_HOME");

        yield return WaitForCondition("Waiting for frames produced by the screen changing",
            () => Runtime.LiveStream.FramesReceived > framesBefore + 5,
            20,
            () => Runtime.LiveStream.Errors);

        Log($"Received {Runtime.LiveStream.FramesReceived - framesBefore} frames while the screen was changing");
        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);
        Assert.IsTrue(Runtime.LiveStream.StopStreaming());
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

    [UnityTest]
    public IEnumerator CanRestartStreaming()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Runtime.LiveStream.StartStreaming(Device, null, maxSize: kMaxSize, maxFps: kMaxFps);

            yield return WaitForCondition($"Waiting for a frame on attempt {attempt + 1}",
                () => Runtime.LiveStream.FramesReceived > 0,
                30,
                () => Runtime.LiveStream.Errors);

            Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);
            Assert.IsTrue(Runtime.LiveStream.StopStreaming());
        }
    }

    [UnityTest]
    public IEnumerator LiveStreamHandlesUnknownDisplay()
    {
        var result = AndroidLogcatLiveStream.Result.Success;
        Runtime.LiveStream.StartStreaming(Device, r => result = r,
            maxSize: kMaxSize, maxFps: kMaxFps, displayId: "12345");

        yield return WaitForCondition("Waiting for the stream to fail",
            () => result == AndroidLogcatLiveStream.Result.Failure, 30);

        var errors = Runtime.LiveStream.Errors;
        Assert.Greater(errors.Length, 0, "Expected an error explaining why the stream failed");
        Assert.IsFalse(Runtime.LiveStream.IsStreaming);

        Log(errors);
        File.WriteAllText(Path.Combine(GetOrCreateArtifactsPath(), "errors.txt"), errors);
    }
}
