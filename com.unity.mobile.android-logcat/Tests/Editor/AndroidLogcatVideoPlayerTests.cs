using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using State = Unity.Android.Logcat.AndroidLogcatVideoPlayer.State;

/// <summary>
/// The capture list's video player against a real screenrecord recording: 137 frames
/// at a variable rate, a single keyframe, so its frame numbers only line up with
/// VideoPlayer's own if the file's frame times are used.
/// <para>
/// The recording lives in the test project rather than the package, which is where CI
/// runs these. Anywhere else the tests that need it are ignored.
/// </para>
/// </summary>
class AndroidLogcatVideoPlayerTests
{
    const string kVideoRelativePath = "Tests/TestVideo.mp4";
    const int kFrameCount = 137;
    // Decoding from the single keyframe to the far end of the recording is the slowest
    // thing here, and well within this on any machine.
    const double kTimeout = 10;

    static string VideoPath => Path.Combine(Application.dataPath, kVideoRelativePath).Replace("\\", "/");

    AndroidLogcatVideoPlayer m_Player;

    [SetUp]
    public void SetUp()
    {
        if (!File.Exists(VideoPath))
            Assert.Ignore($"'{VideoPath}' is not in this project.");
    }

    [TearDown]
    public void TearDown()
    {
        m_Player?.Dispose();
        m_Player = null;
    }

    [Test]
    public void FrameTimesAreReadFromTheFile()
    {
        var times = AndroidLogcatVideoInformation.ReadFrameTimes(VideoPath);

        Assert.IsNotNull(times, "The recording's frame times could not be read.");
        Assert.AreEqual(kFrameCount, times.Length);
        // From a dump of the file's own timing table.
        Assert.AreEqual(0.000, times[0], 0.0005);
        Assert.AreEqual(0.044, times[1], 0.0005);
        Assert.AreEqual(0.261, times[6], 0.0005);
        // 100 ms after frame 6: VideoPlayer's constant-rate numbering skips here.
        Assert.AreEqual(0.361, times[7], 0.0005);
        Assert.AreEqual(4.919, times[kFrameCount - 1], 0.0005);
        for (var i = 1; i < times.Length; i++)
            Assert.Greater(times[i], times[i - 1], $"Frame {i} does not start after frame {i - 1}.");
    }

    [Test]
    public void FrameTimesAreNullForWhatIsNotAnMp4()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{nameof(AndroidLogcatVideoPlayerTests)}_{Guid.NewGuid():N}.mp4");
        try
        {
            File.WriteAllText(path, "This is not a video.");
            Assert.IsNull(AndroidLogcatVideoInformation.Load(path));
            Assert.IsNull(AndroidLogcatVideoInformation.Load(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // VideoPlayer's grid for this recording: its frame 1 is reported at 0.036 s.
    const double kPlayerFrameRate = 1 / 0.036;

    static AndroidLogcatVideoInformation LoadInformation()
    {
        var information = AndroidLogcatVideoInformation.Load(VideoPath);
        Assert.IsNotNull(information, "The recording's frame times could not be read.");
        return information;
    }

    [Test]
    public void FrameAtPicksTheNearestFrame()
    {
        var information = LoadInformation();

        // Where VideoPlayer reports frames 1 and 2, which start at 0.044 s and 0.095 s.
        Assert.AreEqual(1, information.FrameAt(0.036));
        Assert.AreEqual(2, information.FrameAt(0.108));
        Assert.AreEqual(0, information.FrameAt(0));
        Assert.AreEqual(0, information.FrameAt(-1));
        Assert.AreEqual(kFrameCount - 1, information.FrameAt(1000));
        for (var i = 0; i < kFrameCount; i++)
            Assert.AreEqual(i, information.FrameAt(information.TimeOf(i)), $"Frame {i}'s own start time.");
    }

    [Test]
    public void SlotOfIsVideoPlayersOwnFrameNumber()
    {
        var information = LoadInformation();

        // As VideoPlayer reported them: frame 1 in slot 1, frame 2 in slot 3.
        Assert.AreEqual(0, information.SlotOf(0, kPlayerFrameRate));
        Assert.AreEqual(1, information.SlotOf(1, kPlayerFrameRate));
        Assert.AreEqual(3, information.SlotOf(2, kPlayerFrameRate));
        for (var i = 0; i < kFrameCount; i++)
        {
            var slot = information.SlotOf(i, kPlayerFrameRate);
            Assert.AreEqual(information.Snap(i, kPlayerFrameRate), information.FrameAt(slot / kPlayerFrameRate),
                $"Slot {slot} should show what frame {i} snaps to.");
        }
    }

    [Test]
    public void StepFromSkipsFramesThatShareASlot()
    {
        var information = LoadInformation();
        var showable = information.Showable(kPlayerFrameRate);

        var gap = -1;
        for (var i = 0; i + 1 < showable.Length && gap < 0; i++)
        {
            if (showable[i + 1] - showable[i] > 1)
                gap = i;
        }
        Assert.GreaterOrEqual(gap, 0, "This recording has frames close enough to share a slot.");

        Assert.AreEqual(showable[gap + 1], information.StepFrom(showable[gap], 1, kPlayerFrameRate));
        Assert.AreEqual(showable[gap], information.StepFrom(showable[gap + 1], -1, kPlayerFrameRate));
        Assert.AreEqual(-1, information.StepFrom(showable[0], -1, kPlayerFrameRate));
        Assert.AreEqual(-1, information.StepFrom(showable[showable.Length - 1], 1, kPlayerFrameRate));
    }

    [Test]
    public void SnapLandsOnAShowableFrame()
    {
        var information = LoadInformation();
        var showable = information.Showable(kPlayerFrameRate);

        for (var i = 0; i < kFrameCount; i++)
        {
            var snapped = information.Snap(i, kPlayerFrameRate);
            Assert.That(showable, Has.Member(snapped), $"Frame {i} snapped to {snapped}, which cannot be shown.");
            Assert.LessOrEqual(Math.Abs(snapped - i), 1, $"Frame {i} snapped further than its neighbour.");
        }
        foreach (var frame in showable)
            Assert.AreEqual(frame, information.Snap(frame, kPlayerFrameRate), "A showable frame snaps to itself.");
    }

    [Test]
    public void WithoutTheFrameRateEveryFrameCounts()
    {
        var information = LoadInformation();

        Assert.IsNull(information.Showable(0));
        Assert.AreEqual(10, information.Snap(10, 0));
        Assert.AreEqual(11, information.StepFrom(10, 1, 0));
        Assert.AreEqual(9, information.StepFrom(10, -1, 0));
        Assert.AreEqual(-1, information.StepFrom(0, -1, 0));
        Assert.AreEqual(-1, information.StepFrom(kFrameCount - 1, 1, 0));
    }

    [UnityTest]
    public IEnumerator OpensPausedOnTheFirstFrame()
    {
        yield return Open();

        Assert.IsFalse(m_Player.IsPlaying());
        Assert.AreEqual(0, m_Player.Frame, Describe());
        Assert.AreEqual(kFrameCount, m_Player.FrameCount, "The frame count should come from the file, not VideoPlayer's estimate.");
        Assert.IsNotNull(m_Player.Texture, "Opened paused, but with nothing to show.");
        Assert.IsTrue(m_Player.IsShowing(VideoPath));
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

        var showable = ShowableFrames();

        // Across 6 to 8, where one real frame lasts as long as two to three of
        // VideoPlayer's constant-rate ones.
        for (var i = 1; i <= 9; i++)
        {
            m_Player.Step(1);
            yield return WaitForPaused($"stepping forward to {showable[i]}");
            Assert.AreEqual(showable[i], m_Player.Frame, Describe());
        }

        for (var i = 8; i >= 5; i--)
        {
            m_Player.Step(-1);
            yield return WaitForPaused($"stepping back to {showable[i]}");
            Assert.AreEqual(showable[i], m_Player.Frame, Describe());
        }
    }

    /// <summary>
    /// Every frame VideoPlayer can show, one step at a time, including the ones next
    /// to a frame it cannot - which a step has to skip without going further.
    /// </summary>
    [UnityTest]
    public IEnumerator StepsThroughEveryShowableFrame()
    {
        yield return Open();
        var showable = ShowableFrames();

        for (var i = 1; i < showable.Length; i++)
        {
            m_Player.Step(1);
            yield return WaitForPaused($"stepping forward to {showable[i]}");
            Assert.AreEqual(showable[i], m_Player.Frame, Describe());
        }
    }

    [UnityTest]
    public IEnumerator ShowableFramesAreMostOfThem()
    {
        yield return Open();
        var showable = ShowableFrames();

        Assert.AreEqual(0, showable[0]);
        Assert.AreEqual(kFrameCount - 1, showable[showable.Length - 1]);
        // Frames closer together than one slot of VideoPlayer's grid share it, and
        // this recording has between 14 and 17 of those, depending on the exact rate.
        Assert.That(showable.Length, Is.InRange(kFrameCount - 20, kFrameCount - 10), Describe());
        for (var i = 1; i < showable.Length; i++)
            Assert.Greater(showable[i], showable[i - 1]);
    }

    [UnityTest]
    public IEnumerator StepStopsAtEitherEnd()
    {
        yield return Open();

        m_Player.Step(-1);
        Assert.AreEqual(State.Paused, m_Player.CurrentState, "Stepping back from the first frame should do nothing.");
        Assert.AreEqual(0, m_Player.Frame);

        var last = ShowableFrames()[ShowableFrames().Length - 1];
        m_Player.SeekTo(last);
        yield return WaitForPaused("the seek to the last frame");
        Assert.AreEqual(last, m_Player.Frame, Describe());

        m_Player.Step(1);
        Assert.AreEqual(State.Paused, m_Player.CurrentState, "Stepping forward from the last frame should do nothing.");
        Assert.AreEqual(last, m_Player.Frame);

        // Back from the end, twice: a video paused on its last frame counts as finished,
        // and the seek has to survive Play starting it over.
        var showable = ShowableFrames();
        for (var i = showable.Length - 2; i >= showable.Length - 3; i--)
        {
            m_Player.Step(-1);
            yield return WaitForPaused($"stepping back to {showable[i]}");
            Assert.AreEqual(showable[i], m_Player.Frame, Describe());
        }
    }

    [UnityTest]
    public IEnumerator SeeksForwardAndBackWhilePaused()
    {
        yield return Open();

        foreach (var target in new long[] { 50, 120, 10, 7, 100 })
        {
            // 50 and 10 are each in a slot with a neighbour that is shown in their place.
            var expected = m_Player.Information.Snap(target, m_Player.FrameRate);
            m_Player.SeekTo(target);
            yield return WaitForPaused($"the seek to {target}");
            Assert.AreEqual(expected, m_Player.Frame, Describe());
            Assert.IsFalse(m_Player.IsPlaying(), "A seek while paused should leave the video paused.");
        }
    }

    [UnityTest]
    public IEnumerator SeeksWhilePlayingAndKeepsPlaying()
    {
        yield return Open();
        m_Player.TogglePlay();

        m_Player.SeekTo(80);
        Assert.AreEqual(State.Seeking, m_Player.CurrentState, $"The seek did not start. {Describe()}");
        // Playing was already the state before the seek, so the frame is what says it landed.
        yield return WaitFor("the seek to reach 80",
            () => m_Player.CurrentState == State.Playing && m_Player.Frame >= 80);
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
        // At most a few frames on from 50 in the time it took to get here, rather than
        // the second's worth that the clock would have run on by.
        Assert.That(m_Player.Frame, Is.InRange(50, 60), Describe());
    }

    [UnityTest]
    public IEnumerator ClosingLeavesNothingOpen()
    {
        yield return Open();

        m_Player.Open(null);
        Assert.AreEqual(State.Closed, m_Player.CurrentState);
        Assert.IsFalse(m_Player.IsPlaying());
        Assert.IsFalse(m_Player.IsShowing(VideoPath));
    }

    // Only the path: changing the file's modification time while it is open is not
    // possible on Windows, where VideoPlayer holds it - which is also why a recording
    // cannot be overwritten behind the window's back there.
    [UnityTest]
    public IEnumerator IsShowingOnlyTheOpenedFile()
    {
        yield return Open();

        Assert.IsTrue(m_Player.IsShowing(VideoPath));
        Assert.IsFalse(m_Player.IsShowing(Path.ChangeExtension(VideoPath, ".other.mp4")),
            "A different file is not the one showing.");
        Assert.IsFalse(m_Player.IsShowing(null));
        Assert.IsFalse(m_Player.IsShowing(string.Empty));
    }

    /// <summary>Windows cannot move a file the player has open.</summary>
    [UnityTest]
    public IEnumerator RenamingThePreviewedRecordingMovesIt()
    {
        var directory = CreateCapturesFolder();
        var runtime = new AndroidLogcatTestRuntime();
        runtime.Initialize();
        var captures = new AndroidLogcatCaptureScreenshot(runtime, () => directory, true);
        var list = new AndroidLogcatCaptureList(runtime, captures, () => {}, nameof(AndroidLogcatVideoPlayerTests));
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
        var list = new AndroidLogcatCaptureList(runtime, captures, () => {}, nameof(AndroidLogcatVideoPlayerTests));
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
        File.Copy(VideoPath, path);
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

    IEnumerator Open(string path = null)
    {
        // Not the Screen Capture window's, which TearDown would otherwise destroy.
        m_Player = new AndroidLogcatVideoPlayer(nameof(AndroidLogcatVideoPlayerTests));
        m_Player.Open(path ?? VideoPath);
        Assert.AreEqual(State.PausingOnFrame, m_Player.CurrentState);
        yield return WaitForPaused("the first frame");
    }

    long[] ShowableFrames()
    {
        var showable = m_Player.Information?.Showable(m_Player.FrameRate);
        Assert.IsNotNull(showable, $"The player should know which frames it can show once it is open. {Describe()}");
        return showable;
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
