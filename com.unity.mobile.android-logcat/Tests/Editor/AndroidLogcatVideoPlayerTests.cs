using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.TestTools;
using State = Unity.Android.Logcat.AndroidLogcatVideoPlayer.State;

/// <summary>
/// The capture list's video player, against videos made here the way recordings are
/// made: H.264 at a constant 30 fps, so VideoPlayer's frame numbers are exact.
/// </summary>
class AndroidLogcatVideoPlayerTests
{
    const int kFrameRate = AndroidLogcatStreamRecorder.kFrameRate;
    const int kFrameCount = 90;
    const int kSize = 128;
    // Decoding to the far end of a video is the slowest thing here, and well within
    // this on any machine.
    const double kTimeout = 10;

    static string s_Folder;
    static string s_VideoPath;
    static string s_SingleFramePath;
    static string s_Unavailable;

    AndroidLogcatVideoPlayer m_Player;

    [OneTimeSetUp]
    public void WriteVideos()
    {
        // The Linux Editor has no H.264 encoder or decoder.
        if (Application.platform == RuntimePlatform.LinuxEditor)
        {
            s_Unavailable = "There is no H.264 in the Linux Editor.";
            return;
        }

        s_Folder = Path.Combine(Path.GetTempPath(), "AndroidLogcatTests", Guid.NewGuid().ToString("N")).Replace("\\", "/");
        Directory.CreateDirectory(s_Folder);
        s_VideoPath = Path.Combine(s_Folder, "video.mp4").Replace("\\", "/");
        s_SingleFramePath = Path.Combine(s_Folder, "single.mp4").Replace("\\", "/");
        try
        {
            WriteVideo(s_VideoPath, kFrameCount);
            WriteVideo(s_SingleFramePath, 1);
        }
        catch (Exception ex)
        {
            s_Unavailable = $"The test videos could not be written: {ex.Message}";
        }
    }

    [OneTimeTearDown]
    public void DeleteVideos()
    {
        if (s_Folder != null && Directory.Exists(s_Folder))
            Directory.Delete(s_Folder, true);
    }

    [SetUp]
    public void SetUp()
    {
        if (s_Unavailable != null && IsPlaybackTest())
            Assert.Ignore(s_Unavailable);
    }

    [TearDown]
    public void TearDown()
    {
        m_Player?.Dispose();
        m_Player = null;
    }

    bool IsPlaybackTest()
    {
        var method = GetType().GetMethod(TestContext.CurrentContext.Test.MethodName);
        return method != null && method.IsDefined(typeof(UnityTestAttribute), false);
    }

    /// <summary>A frame a shade lighter each time, so consecutive frames differ.</summary>
    static void WriteVideo(string path, int frames)
    {
        var h264 = new H264EncoderAttributes
        {
            gopSize = kFrameRate,
            numConsecutiveBFrames = 0,
            profile = VideoEncodingProfile.H264Baseline
        };
        var attributes = new VideoTrackEncoderAttributes(h264)
        {
            frameRate = new MediaRational(kFrameRate),
            width = kSize,
            height = kSize,
            includeAlpha = false,
            targetBitRate = 1000000
        };
        var texture = new Texture2D(kSize, kSize, TextureFormat.RGBA32, false);
        try
        {
            using (var encoder = new MediaEncoder(path, attributes))
            {
                var pixels = new Color32[kSize * kSize];
                for (var i = 0; i < frames; i++)
                {
                    var shade = (byte)(i * 255 / Math.Max(1, frames - 1));
                    for (var p = 0; p < pixels.Length; p++)
                        pixels[p] = new Color32(shade, (byte)(255 - shade), (byte)(p % kSize * 2), 255);
                    texture.SetPixels32(pixels);
                    texture.Apply(false);
                    if (!encoder.AddFrame(texture))
                        throw new Exception($"Frame {i} was refused.");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void FormatsTimeAsMinutesSecondsAndHundredths()
    {
        Assert.AreEqual("0:00.00", AndroidLogcatVideoPlayer.FormatTime(0));
        Assert.AreEqual("0:03.00", AndroidLogcatVideoPlayer.FormatTime(3));
        Assert.AreEqual("0:00.03", AndroidLogcatVideoPlayer.FormatTime(1.0 / 30));
        Assert.AreEqual("1:05.34", AndroidLogcatVideoPlayer.FormatTime(65.338));
        Assert.AreEqual("1:00.00", AndroidLogcatVideoPlayer.FormatTime(59.996));
        Assert.AreEqual("0:00.00", AndroidLogcatVideoPlayer.FormatTime(-1));
        Assert.AreEqual("4.60", AndroidLogcatVideoPlayer.FormatTime(4.6, false));
        Assert.AreEqual("0.03", AndroidLogcatVideoPlayer.FormatTime(1.0 / 30, false));
    }

    [UnityTest]
    public IEnumerator OpensPausedOnTheFirstFrame()
    {
        yield return Open();

        Assert.IsFalse(m_Player.IsPlaying());
        Assert.AreEqual(0, m_Player.Frame, Describe());
        Assert.AreEqual(kFrameCount, m_Player.FrameCount, Describe());
        Assert.AreEqual(kFrameRate, m_Player.FrameRate, 0.01, Describe());
        Assert.AreEqual(kFrameCount / (double)kFrameRate, m_Player.Length, 0.01);
        Assert.AreEqual(1.0, m_Player.TimeOf(kFrameRate), 0.001);
        Assert.IsNotNull(m_Player.Texture, "Opened paused, but with nothing to show.");
        Assert.IsTrue(m_Player.IsShowing(s_VideoPath));
    }

    [UnityTest]
    public IEnumerator PlaysAndPauses()
    {
        yield return Open();

        m_Player.TogglePlay();
        Assert.IsTrue(m_Player.IsPlaying());
        yield return WaitFor("playback to move past frame 10", () => m_Player.Frame > 10);

        m_Player.TogglePlay();
        Assert.AreEqual(State.Paused, m_Player.CurrentState, Describe());
        var pausedOn = m_Player.Frame;

        yield return WaitSeconds(0.5);
        Assert.AreEqual(pausedOn, m_Player.Frame, $"Paused, but the frame moved on. {Describe()}");
    }

    [UnityTest]
    public IEnumerator StepsOneFrameAtATimeWhilePaused()
    {
        yield return Open();

        for (var i = 1; i <= 9; i++)
        {
            m_Player.Step(1);
            yield return WaitForPaused($"stepping forward to {i}");
            Assert.AreEqual(i, m_Player.Frame, Describe());
        }

        for (var i = 8; i >= 5; i--)
        {
            m_Player.Step(-1);
            yield return WaitForPaused($"stepping back to {i}");
            Assert.AreEqual(i, m_Player.Frame, Describe());
        }
    }

    [UnityTest]
    public IEnumerator StepsThroughEveryFrame()
    {
        yield return Open();

        for (var i = 1; i < kFrameCount; i++)
        {
            m_Player.Step(1);
            yield return WaitForPaused($"stepping forward to {i}");
            Assert.AreEqual(i, m_Player.Frame, Describe());
        }
    }

    [UnityTest]
    public IEnumerator StepStopsAtEitherEnd()
    {
        yield return Open();

        m_Player.Step(-1);
        Assert.AreEqual(State.Paused, m_Player.CurrentState, "Stepping back from the first frame should do nothing.");
        Assert.AreEqual(0, m_Player.Frame);

        const int last = kFrameCount - 1;
        m_Player.SeekTo(last);
        yield return WaitForPaused("the seek to the last frame");
        Assert.AreEqual(last, m_Player.Frame, Describe());

        m_Player.Step(1);
        Assert.AreEqual(State.Paused, m_Player.CurrentState, "Stepping forward from the last frame should do nothing.");
        Assert.AreEqual(last, m_Player.Frame);

        // Back from the end, twice: a video paused on its last frame counts as finished,
        // and the seek has to survive Play starting it over.
        for (var i = last - 1; i >= last - 2; i--)
        {
            m_Player.Step(-1);
            yield return WaitForPaused($"stepping back to {i}");
            Assert.AreEqual(i, m_Player.Frame, Describe());
        }
    }

    [UnityTest]
    public IEnumerator SeeksForwardAndBackWhilePaused()
    {
        yield return Open();

        foreach (var target in new long[] { 50, 80, 10, 7, 60 })
        {
            m_Player.SeekTo(target);
            yield return WaitForPaused($"the seek to {target}");
            Assert.AreEqual(target, m_Player.Frame, Describe());
            Assert.IsFalse(m_Player.IsPlaying(), "A seek while paused should leave the video paused.");
        }
    }

    [UnityTest]
    public IEnumerator SeeksWhilePlayingAndKeepsPlaying()
    {
        yield return Open();
        m_Player.TogglePlay();

        m_Player.SeekTo(60);
        Assert.AreEqual(State.Seeking, m_Player.CurrentState, $"The seek did not start. {Describe()}");
        // Playing was already the state before the seek, so the frame is what says it landed.
        yield return WaitFor("the seek to reach 60",
            () => m_Player.CurrentState == State.Playing && m_Player.Frame >= 60);
        Assert.IsTrue(m_Player.IsPlaying());
    }

    [UnityTest]
    public IEnumerator PlayResumesFromWhereAPausedSeekLanded()
    {
        yield return Open();

        m_Player.SeekTo(50);
        yield return WaitForPaused("the seek to 50");
        // Long enough for a player clock that kept running while paused to show.
        yield return WaitSeconds(1);

        m_Player.TogglePlay();
        yield return WaitFor("playback to resume", () => m_Player.CurrentState == State.Playing);
        // At most a few frames on from 50, rather than the second's worth that the clock
        // would have run on by.
        Assert.That(m_Player.Frame, Is.InRange(50, 60), Describe());
    }

    [UnityTest]
    public IEnumerator ClosingLeavesNothingOpen()
    {
        yield return Open();

        m_Player.Open(null);
        Assert.AreEqual(State.Closed, m_Player.CurrentState);
        Assert.IsFalse(m_Player.IsPlaying());
        Assert.IsFalse(m_Player.IsShowing(s_VideoPath));
    }

    // Only the path: changing the file's modification time while it is open is not
    // possible on Windows, where VideoPlayer holds it.
    [UnityTest]
    public IEnumerator IsShowingOnlyTheOpenedFile()
    {
        yield return Open();

        Assert.IsTrue(m_Player.IsShowing(s_VideoPath));
        Assert.IsFalse(m_Player.IsShowing(Path.ChangeExtension(s_VideoPath, ".other.mp4")),
            "A different file is not the one showing.");
        Assert.IsFalse(m_Player.IsShowing(null));
        Assert.IsFalse(m_Player.IsShowing(string.Empty));
    }

    /// <summary>The end may come before the frame does, or with no event at all.</summary>
    [UnityTest]
    public IEnumerator OpensASingleFrameRecordingPaused()
    {
        yield return OpenSingleFrame();

        Assert.AreEqual(0, m_Player.Frame, Describe());
        Assert.AreEqual(1, m_Player.FrameCount);
        Assert.IsNotNull(m_Player.Texture, "Opened paused, but with nothing to show.");
    }

    [UnityTest]
    public IEnumerator PlaysAndPausesASingleFrameRecording()
    {
        yield return OpenSingleFrame();

        m_Player.TogglePlay();
        Assert.IsTrue(m_Player.IsPlaying());
        yield return WaitSeconds(0.5);
        Assert.AreNotEqual(State.Failed, m_Player.CurrentState, Describe());

        m_Player.TogglePlay();
        yield return WaitForPaused("pausing");
        Assert.AreEqual(0, m_Player.Frame, Describe());
    }

    [UnityTest]
    public IEnumerator SeekingASingleFrameRecordingStaysOnIt()
    {
        yield return OpenSingleFrame();

        m_Player.SeekTo(0);
        yield return WaitForPaused("seeking to 0");
        Assert.AreEqual(0, m_Player.Frame, Describe());

        m_Player.SeekTo(5);
        yield return WaitForPaused("seeking past the end");
        Assert.AreEqual(0, m_Player.Frame, Describe());
    }

    /// <summary>
    /// A platform that cannot decode a one frame video is fine, as long as it says so
    /// rather than opening forever.
    /// </summary>
    IEnumerator OpenSingleFrame()
    {
        m_Player = new AndroidLogcatVideoPlayer(nameof(AndroidLogcatVideoPlayerTests));
        m_Player.Open(s_SingleFramePath);
        var start = EditorApplication.timeSinceStartup;
        while (m_Player.CurrentState != State.Paused && m_Player.CurrentState != State.Failed)
        {
            if (EditorApplication.timeSinceStartup - start > kTimeout)
                Assert.Fail($"Timed out after {kTimeout} s opening.\n{Describe()}");
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
        }
        if (m_Player.CurrentState == State.Failed)
            Assert.Ignore($"This platform cannot decode the video: {m_Player.Error}");
    }

    /// <summary>Windows cannot move a file the player has open.</summary>
    [UnityTest]
    public IEnumerator RenamingThePreviewedRecordingMovesIt()
    {
        var directory = CreateCapturesFolder();
        var runtime = new AndroidLogcatTestRuntime();
        runtime.Initialize();
        var captures = new AndroidLogcatCaptureScreenshot(runtime, () => directory, true);
        var list = new AndroidLogcatCaptureList(runtime, captures, () => { }, nameof(AndroidLogcatVideoPlayerTests));
        try
        {
            var path = CopyRecordingTo(directory);
            yield return Preview(list, captures, path);

            Assert.IsTrue(list.RenameCapture(path, "unittest-recording-renamed"), "The rename should have succeeded.");
            var renamed = Path.Combine(directory, "unittest-recording-renamed.mp4").Replace("\\", "/");
            FileAssert.Exists(renamed);
            FileAssert.DoesNotExist(path);

            list.SyncPreview();
            Assert.AreEqual(renamed, list.PreviewPath, "The preview should follow the rename.");
            yield return WaitForPaused("the renamed recording to open");
            Assert.IsTrue(m_Player.IsShowing(renamed));
        }
        finally
        {
            list.Dispose();
            runtime.Shutdown();
            Directory.Delete(directory, true);
        }
    }

    [UnityTest]
    public IEnumerator DeletingThePreviewedRecordingRemovesIt()
    {
        var directory = CreateCapturesFolder();
        var runtime = new AndroidLogcatTestRuntime();
        runtime.Initialize();
        var captures = new AndroidLogcatCaptureScreenshot(runtime, () => directory, true);
        var list = new AndroidLogcatCaptureList(runtime, captures, () => { }, nameof(AndroidLogcatVideoPlayerTests));
        try
        {
            var path = CopyRecordingTo(directory);
            yield return Preview(list, captures, path);

            list.DeleteCaptures(captures.GetScreenshots(), new[] { path }, null);
            FileAssert.DoesNotExist(path);

            list.SyncPreview();
            Assert.AreEqual(State.Closed, m_Player.CurrentState, "Nothing is left to preview.");
        }
        finally
        {
            list.Dispose();
            runtime.Shutdown();
            Directory.Delete(directory, true);
        }
    }

    static string CreateCapturesFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AndroidLogcatTests", Guid.NewGuid().ToString("N")).Replace("\\", "/");
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>A copy the test can move and delete.</summary>
    static string CopyRecordingTo(string directory)
    {
        var path = Path.Combine(directory, "unittest-recording_1.mp4").Replace("\\", "/");
        File.Copy(s_VideoPath, path);
        return path;
    }

    /// <summary>Selects the recording and waits until it is open.</summary>
    IEnumerator Preview(AndroidLogcatCaptureList list, AndroidLogcatCaptureScreenshot captures, string path)
    {
        captures.SelectImage(path);
        list.SyncPreview();
        Assert.AreEqual(path, list.PreviewPath);
        m_Player = list.VideoPlayer;
        yield return WaitForPaused("the recording to open");
        Assert.IsTrue(m_Player.IsShowing(path));
    }

    IEnumerator Open()
    {
        // Not the Screen Capture window's, which TearDown would otherwise destroy.
        m_Player = new AndroidLogcatVideoPlayer(nameof(AndroidLogcatVideoPlayerTests));
        m_Player.Open(s_VideoPath);
        Assert.AreEqual(State.PausingOnFrame, m_Player.CurrentState);
        yield return WaitForPaused("the first frame");
    }

    IEnumerator WaitForPaused(string what)
    {
        return WaitFor(what, () => m_Player.CurrentState == State.Paused);
    }

    /// <summary>
    /// The player only moves on when the player loop runs, which in edit mode is not
    /// every Editor update unless asked for.
    /// </summary>
    IEnumerator WaitFor(string what, Func<bool> done)
    {
        var start = EditorApplication.timeSinceStartup;
        while (!done())
        {
            if (m_Player.CurrentState == State.Failed)
                Assert.Fail($"The player failed while waiting for {what}: {m_Player.Error}\n{Describe()}");
            if (EditorApplication.timeSinceStartup - start > kTimeout)
                Assert.Fail($"Timed out after {kTimeout} s waiting for {what}.\n{Describe()}");
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
        }
    }

    IEnumerator WaitSeconds(double seconds)
    {
        var start = EditorApplication.timeSinceStartup;
        while (EditorApplication.timeSinceStartup - start < seconds)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
        }
    }

    /// <summary>The same text as the debug panel's Copy button, for a failure message.</summary>
    string Describe() => m_Player != null ? m_Player.GetDebugInfo() : "No player.";
}
