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

        // Every test here pushes the server to the device, so without a jar the whole
        // fixture fails one slow timeout at a time, saying nothing useful. It is a
        // build output and is not committed, so a fresh clone has none yet.
        FileAssert.Exists(AndroidLogcatLiveStream.GetServerJarPath(),
            "Build the live stream server by running 'gradlew dexJar' in External/UnityLogcatServer.");
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

        // The texture, not the frame count, because this test is about the texture
        // being there to draw. Everything else uses WaitForFirstFrame.
        yield return WaitForCondition("Waiting for the first frame",
            () => Runtime.LiveStream.Texture != null,
            kDefaultTimeout,
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
        ReportArtifact("frame.png", texture);

        // Note there is deliberately no assertion about a frame rate here. A mirrored
        // display only produces a buffer when the screen changes, so a device sitting
        // on a static screen legitimately sends nothing at all - see
        // StreamsFramesWhileScreenChanges. What matters here is that the stream stays
        // up rather than dying once the first frame is through.
        yield return WaitFor(2.0, "Letting the stream run");

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

        yield return WaitForFirstFrame();

        var framesBefore = Runtime.LiveStream.FramesReceived;

        // Each of these animates for a few hundred milliseconds. They are sent before
        // waiting rather than interleaved because the reader thread counts frames on
        // its own, so the count has already moved by the time we look.
        SendKeyEvent("KEYCODE_APP_SWITCH");
        SendKeyEvent("KEYCODE_HOME");
        SendKeyEvent("KEYCODE_APP_SWITCH");
        SendKeyEvent("KEYCODE_HOME");

        yield return WaitForMoreFrames("Waiting for frames produced by the screen changing",
            framesBefore, 5);

        Log($"Received {Runtime.LiveStream.FramesReceived - framesBefore} frames while the screen was changing");
        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);
        Assert.IsTrue(Runtime.LiveStream.StopStreaming());
    }

    /// <summary>
    /// Injected touch is verified by its effect: a swipe up from the bottom of the
    /// screen changes what is displayed, and nothing else is touching the device, so
    /// frames arriving afterwards can only be the result of our own gesture.
    /// </summary>
    [UnityTest]
    public IEnumerator CanSendTouchToDevice()
    {
        // Start from the home screen so the swipe has something to act on.
        SendKeyEvent("KEYCODE_HOME");

        Runtime.LiveStream.StartStreaming(Device, null, maxSize: kMaxSize, maxFps: kMaxFps);

        yield return WaitForFirstFrame();

        Assert.IsTrue(Runtime.LiveStream.ControlSupported,
            "Expected the server to report that it can inject input");

        // Let the home screen settle, so the frames counted below are the swipe's.
        yield return WaitFor(1.5, "Letting the screen settle");

        var framesBefore = Runtime.LiveStream.FramesReceived;
        Runtime.LiveStream.SendTouch(AndroidLogcatLiveStream.TouchAction.Down, 0.5f, 0.85f);
        for (var step = 1; step <= 10; step++)
        {
            Runtime.LiveStream.SendTouch(AndroidLogcatLiveStream.TouchAction.Move,
                0.5f, 0.85f - 0.55f * step / 10.0f);
            yield return Waiting();
        }
        Runtime.LiveStream.SendTouch(AndroidLogcatLiveStream.TouchAction.Up, 0.5f, 0.30f);

        yield return WaitForMoreFrames("Waiting for the screen to react to the injected swipe", framesBefore, 5);

        Log($"Injected swipe produced {Runtime.LiveStream.FramesReceived - framesBefore} frames");
        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);

        var texture = Runtime.LiveStream.Texture;
        ReportArtifact("after-swipe.png", texture);

        Assert.IsTrue(Runtime.LiveStream.StopStreaming());
        SendKeyEvent("KEYCODE_HOME");
    }

    /// <summary>
    /// Same reasoning as the touch test: an injected key changes what is on screen, and
    /// nothing else is touching the device, so the frames that follow are its effect.
    /// </summary>
    [UnityTest]
    public IEnumerator CanSendKeysToDevice()
    {
        SendKeyEvent("KEYCODE_HOME");

        Runtime.LiveStream.StartStreaming(Device, null, maxSize: kMaxSize, maxFps: kMaxFps);

        yield return WaitForFirstFrame();

        Assert.IsTrue(Runtime.LiveStream.ControlSupported,
            "Expected the server to report that it can inject input");

        yield return WaitFor(1.5, "Letting the screen settle");

        // Recents animates in, so it is a visible effect that needs no app installed.
        var framesBefore = Runtime.LiveStream.FramesReceived;
        // SendKeyPress is what the toolbar's Back / Home / Recents buttons call.
        Runtime.LiveStream.SendKeyPress(AndroidKeyCode.APP_SWITCH);

        yield return WaitForMoreFrames("Waiting for the screen to react to the injected key", framesBefore, 5);

        Log($"Injected key produced {Runtime.LiveStream.FramesReceived - framesBefore} frames");

        // Text goes through a different path on the device - KeyCharacterMap rather than
        // a keycode - so it is worth exercising separately.
        framesBefore = Runtime.LiveStream.FramesReceived;
        Runtime.LiveStream.SendText("unity");

        yield return WaitForMoreFrames("Waiting for the screen to react to injected text", framesBefore, 2);

        Log($"Injected text produced {Runtime.LiveStream.FramesReceived - framesBefore} frames");
        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);

        ReportArtifact("after-keys.png", Runtime.LiveStream.Texture);

        Assert.IsTrue(Runtime.LiveStream.StopStreaming());
        SendKeyEvent("KEYCODE_HOME");
    }

    /// <summary>
    /// Scroll goes in as <c>ACTION_SCROLL</c> from a mouse source, which is a different
    /// path on the device again - and one that needs a hover in front of it, see
    /// `ScrollInjector`. Settings stands in for "something long enough to scroll",
    /// since it is on every device.
    /// </summary>
    [UnityTest]
    public IEnumerator CanScrollDeviceScreen()
    {
        Device.ActivityManager.StartOrResumePackage("com.android.settings");

        Runtime.LiveStream.StartStreaming(Device, null, maxSize: kMaxSize, maxFps: kMaxFps);

        yield return WaitForFirstFrame();

        Assert.IsTrue(Runtime.LiveStream.ControlSupported,
            "Expected the server to report that it can inject input");

        yield return WaitFor(2.0, "Letting Settings settle");

        var framesBefore = Runtime.LiveStream.FramesReceived;

        // Left of centre and low down, which is inside the list on every device tried -
        // the middle of the screen can be covered by a picture-in-picture window, which
        // reacts to the hover and then has nothing to scroll.
        for (var i = 0; i < 5; i++)
            Runtime.LiveStream.SendScroll(0.3f, 0.7f, 0f, -3f);

        yield return WaitForMoreFrames("Waiting for the screen to react to the injected scroll", framesBefore, 3);

        Log($"Injected scroll produced {Runtime.LiveStream.FramesReceived - framesBefore} frames");
        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);

        // Written out because a frame count only says the screen changed, not that it
        // scrolled - the artifact is what shows the list moved.
        ReportArtifact("after-scroll.png", Runtime.LiveStream.Texture);

        Assert.IsTrue(Runtime.LiveStream.StopStreaming());
        SendKeyEvent("KEYCODE_HOME");
    }

    /// <summary>
    /// A device whose screen is off composes nothing, so a mirrored display produces no
    /// frames and the view sits blank - which is why starting a stream wakes it. The
    /// fixture wakes the device before every test, so this one puts it back to sleep to
    /// have something to prove.
    /// </summary>
    [UnityTest]
    public IEnumerator StreamsAfterWakingASleepingDevice()
    {
        Device.Sleep();

        yield return WaitFor(1.5, "Letting the device fall asleep");

        Runtime.LiveStream.StartStreaming(Device, null, maxSize: kMaxSize, maxFps: kMaxFps);

        yield return WaitForFirstFrame("Waiting for a frame from a device that was asleep");

        Assert.AreEqual(string.Empty, Runtime.LiveStream.Errors);
        Assert.IsTrue(Runtime.LiveStream.StopStreaming());
    }

    /// <summary>
    /// Waits for the stream to deliver its first frame, which is the earliest a test
    /// can tell that the server is up, connected and mirroring.
    /// <para>
    /// Frames rather than <c>Texture</c>: the texture outlives a stream, so a restart
    /// would see the previous one and wait for nothing. The frame count is reset by
    /// every start.
    /// </para>
    /// <para>
    /// Returns the wait instead of yielding it, so that callers keep the single level
    /// of enumerator the test runner drives.
    /// </para>
    /// </summary>
    private IEnumerator WaitForFirstFrame(string what = "Waiting for the first frame")
    {
        return WaitForCondition(what,
            () => Runtime.LiveStream.FramesReceived > 0,
            kDefaultTimeout,
            () => Runtime.LiveStream.Errors);
    }

    /// <summary>
    /// Waits for the screen to produce another <paramref name="count"/> frames, which
    /// is how a test sees that something it injected actually did something. The device
    /// only sends a frame when the screen changes, so this is the effect, not a clock.
    /// </summary>
    private IEnumerator WaitForMoreFrames(string what, int framesBefore, int count)
    {
        return WaitForCondition(what,
            () => Runtime.LiveStream.FramesReceived > framesBefore + count,
            kDefaultTimeout,
            () => $"Frames before {framesBefore}, now {Runtime.LiveStream.FramesReceived}. " +
                $"{Runtime.LiveStream.Errors}");
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

            yield return WaitForFirstFrame($"Waiting for a frame on attempt {attempt + 1}");

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
            () => result == AndroidLogcatLiveStream.Result.Failure, kDefaultTimeout);

        var errors = Runtime.LiveStream.Errors;
        Assert.Greater(errors.Length, 0, "Expected an error explaining why the stream failed");
        Assert.IsFalse(Runtime.LiveStream.IsStreaming);

        Log(errors);
        ReportArtifact("errors.txt", errors);
    }
}
