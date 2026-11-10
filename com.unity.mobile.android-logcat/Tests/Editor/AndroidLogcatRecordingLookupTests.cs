using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// What the details column and the touch marker look up on every repaint of a playing
/// recording, near the end of the longest one the time limit allows, with a finger on
/// the screen the whole time.
/// </summary>
class AndroidLogcatRecordingLookupTests
{
    const int kFrameRate = 30;
    const int kMinutes = 60;
    const int kVideoSide = 1024;
    // A repaint at 60 Hz has 16 ms, and these lookups are a small part of one.
    const double kBudgetMs = 0.5;
    const int kRepaints = 200;

    string m_File;

    [SetUp]
    public void SetUp()
    {
        m_File = Path.GetTempFileName();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(m_File))
            File.Delete(m_File);
    }

    [Test]
    public void LookupsNearTheEndOfALongRecordingStayWithinBudget()
    {
        var frames = kFrameRate * 60 * kMinutes;
        var changes = new AndroidLogcatScreenshotInfo.FrameChange[frames];
        for (var i = 0; i < frames; i++)
        {
            var change = new AndroidLogcatScreenshotInfo.FrameChange { time = (double)i / kFrameRate };
            var action = i == 0 ? AndroidLogcatLiveStream.TouchAction.Down : AndroidLogcatLiveStream.TouchAction.Move;
            change.touches.Add(new AndroidLogcatScreenshotInfo.TouchInfo { action = action.ToString(), x = (i % 100) / 100f, y = 0.5f });
            changes[i] = change;
        }
        // Folded at the start, unfolded at a minute in, then only touches: the slowest
        // case for finding the latest display change.
        changes[0].display = new AndroidLogcatScreenshotInfo.DisplayInfo { width = 1080, height = 2424 };
        changes[0].content = new AndroidLogcatScreenshotInfo.ContentInfo { x = 284, y = 0, width = 456, height = 1024 };
        var unfold = kFrameRate * 60;
        changes[unfold].display = new AndroidLogcatScreenshotInfo.DisplayInfo { width = 2076, height = 2152 };
        changes[unfold].content = new AndroidLogcatScreenshotInfo.ContentInfo { x = 18, y = 0, width = 988, height = 1024 };

        var info = new AndroidLogcatScreenshotInfo { displayWidth = 1080, displayHeight = 2424, frameChanges = changes };
        var json = JsonUtility.ToJson(info);
        var load = Stopwatch.StartNew();
        info = JsonUtility.FromJson<AndroidLogcatScreenshotInfo>(json);
        load.Stop();
        var details = new AndroidLogcatCaptureList.PreviewDetails(null, new FileInfo(m_File), info);

        var end = (double)(frames - 1) / kFrameRate;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < kRepaints; i++)
        {
            var time = end - i * 0.001;
            details.DisplaySizeAt(time);
            details.ContentAt(time, kVideoSide, kVideoSide);
            details.TouchAt(time, 0.3, out _, out _, out _);
        }
        clock.Stop();
        var perRepaint = clock.Elapsed.TotalMilliseconds / kRepaints;
        Debug.Log($"{frames} changes, {json.Length / (1024 * 1024)} MB of json loaded in {load.Elapsed.TotalMilliseconds:F0} ms, "
            + $"lookups per repaint {perRepaint:F3} ms");

        Assert.AreEqual("2076x2152", details.DisplaySizeAt(end));
        Assert.AreEqual(18 / (float)kVideoSide + 1f / kVideoSide, details.ContentAt(end, kVideoSide, kVideoSide).x, 1e-6f);
        Assert.IsTrue(details.TouchAt(end, 0.3, out _, out var alpha, out var touchAction));
        Assert.AreEqual(1f, alpha);
        Assert.AreEqual(nameof(AndroidLogcatLiveStream.TouchAction.Move), touchAction);
        Assert.Less(perRepaint, kBudgetMs, "Looking up a recording's changes takes too long for a repaint");
    }
}
