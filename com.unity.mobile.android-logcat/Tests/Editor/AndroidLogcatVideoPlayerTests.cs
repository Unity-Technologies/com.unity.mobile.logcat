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
        var times = AndroidLogcatMp4Timing.ReadFrameTimes(VideoPath);

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
            Assert.IsNull(AndroidLogcatMp4Timing.ReadFrameTimes(path));
            Assert.IsNull(AndroidLogcatMp4Timing.ReadFrameTimes(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
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

        // Across 6 to 8, where one real frame lasts as long as two to three of
        // VideoPlayer's constant-rate ones.
        for (var expected = 1; expected <= 9; expected++)
        {
            m_Player.Step(1);
            yield return WaitForPaused($"stepping forward to {expected}");
            Assert.AreEqual(expected, m_Player.Frame, Describe());
        }

        for (var expected = 8; expected >= 5; expected--)
        {
            m_Player.Step(-1);
            yield return WaitForPaused($"stepping back to {expected}");
            Assert.AreEqual(expected, m_Player.Frame, Describe());
        }
    }

    [UnityTest]
    public IEnumerator StepStopsAtEitherEnd()
    {
        yield return Open();

        m_Player.Step(-1);
        Assert.AreEqual(State.Paused, m_Player.CurrentState, "Stepping back from the first frame should do nothing.");
        Assert.AreEqual(0, m_Player.Frame);

        m_Player.SeekTo(kFrameCount - 1);
        yield return WaitForPaused("the seek to the last frame");
        Assert.AreEqual(kFrameCount - 1, m_Player.Frame, Describe());

        m_Player.Step(1);
        Assert.AreEqual(State.Paused, m_Player.CurrentState, "Stepping forward from the last frame should do nothing.");
        Assert.AreEqual(kFrameCount - 1, m_Player.Frame);
    }

    [UnityTest]
    public IEnumerator SeeksForwardAndBackWhilePaused()
    {
        yield return Open();

        foreach (var target in new long[] { 50, 120, 10, 7, 100 })
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

    IEnumerator Open(string path = null)
    {
        m_Player = new AndroidLogcatVideoPlayer();
        m_Player.Open(path ?? VideoPath);
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
