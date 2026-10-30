using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The recorder driven directly, the way the live stream drives it but without a
/// device: frames are queued, and nothing decodes them until Stop, as when the
/// Editor falls behind.
/// </summary>
class AndroidLogcatStreamRecorderTests
{
    const int kFrameRate = 30;
    const long kFrameUs = 1000000 / kFrameRate;

    string m_Folder;
    AndroidLogcatStreamRecorder m_Recorder;

    [SetUp]
    public void SetUp()
    {
        // The Linux Editor has no H.264 encoder.
        if (Application.platform == RuntimePlatform.LinuxEditor)
            Assert.Ignore("There is no H.264 in the Linux Editor.");

        m_Folder = Path.Combine(Path.GetTempPath(), "AndroidLogcatTests", Guid.NewGuid().ToString("N")).Replace("\\", "/");
        Directory.CreateDirectory(m_Folder);
    }

    [TearDown]
    public void TearDown()
    {
        // Only a test that failed before stopping it leaves one, and its file has to be
        // closed before it can be deleted.
        m_Recorder?.Stop();
        m_Recorder = null;
        if (m_Folder != null && Directory.Exists(m_Folder))
            Directory.Delete(m_Folder, true);
    }

    [Test]
    public void ChangesOfAQueuedFrameKeepTheirTimeAfterALaterTouch()
    {
        // Landscape, then portrait a frame later - a fold or a turn - both still queued.
        // Encoded first: the recording's clock starts with the recorder.
        var landscape = Jpeg(128, 64);
        var portrait = Jpeg(64, 128);
        m_Recorder = new AndroidLogcatStreamRecorder(Path.Combine(m_Folder, "touch.mp4"), kFrameRate,
            null, 0, Vector2Int.zero, rotation => 0);
        m_Recorder.Enqueue(landscape, landscape.Length, 0, 0, 2424, 1080);
        m_Recorder.Enqueue(portrait, portrait.Length, kFrameUs, 0, 1080, 2424);

        // Well past both frames' slots, on the recording's clock.
        Thread.Sleep(300);
        m_Recorder.NoteTouch(AndroidLogcatLiveStream.TouchAction.Down, 0.5f, 0.5f);

        // Decodes the queued frames, whose changes are timed earlier than the touch.
        var recorder = m_Recorder;
        m_Recorder = null;
        Assert.IsTrue(recorder.Stop(), $"Nothing was recorded. {recorder.Error}");

        var changes = recorder.FrameChanges;
        var times = changes.Select(change => change.time).ToArray();
        Assert.That(times, Is.Ordered.Ascending.And.Unique, $"Changes out of order: {string.Join(", ", times)}");

        var turned = changes.Single(change => change.display.IsSet && change.display.width == 1080);
        var touch = changes.Single(change => change.touches.Count > 0);
        // A frame after the first, give or take how long queueing took.
        Assert.That(turned.time, Is.InRange(1.0 / kFrameRate, 3.0 / kFrameRate), "The portrait frame's change moved off its own slot");
        Assert.Less(turned.time, touch.time, "The portrait frame's change should come before the touch");
        Assert.IsTrue(turned.content.IsSet, "The portrait frame sits differently in the video, so its crop should change too");
        Assert.IsEmpty(turned.touches, "The touch was merged into the frame's change");
        Assert.IsFalse(touch.display.IsSet, "The frame's display change was merged into the touch");
    }

    static byte[] Jpeg(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)(i % 256), 128, 64, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture.EncodeToJPG();
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
