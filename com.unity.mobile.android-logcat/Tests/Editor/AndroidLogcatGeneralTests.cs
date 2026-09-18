using System;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Android.Logcat;

class AndroidLogcatGeneralTests
{
    /// <summary>
    /// The screenshots folder is an ordinary directory, so files can appear in it or
    /// change without the Editor having done anything, and a cached listing cannot
    /// notice by itself. This is the API underneath the Screen Capture window's
    /// refresh when it regains focus.
    /// <para>
    /// No device needed: the listing is a directory scan, so the files can simply be
    /// written here.
    /// </para>
    /// </summary>
    [Test]
    public void InvalidateScreenshotsPicksUpExternalChanges()
    {
        var runtime = new AndroidLogcatTestRuntime();
        runtime.Initialize();
        try
        {
            // Built directly rather than taken from the runtime: the test runtime has
            // no screen capture service, and the two calls used here only read the
            // directory - nothing is queued, so nothing needs a device or a dispatcher.
            var captureScreenshot = new AndroidLogcatCaptureScreenshot(runtime);

            var directory = AndroidLogcatUtilities.GetScreenshotsDirectory();
            System.IO.Directory.CreateDirectory(directory);

            var first = System.IO.Path.Combine(directory, "unittest-device_1.png").Replace("\\", "/");
            var second = System.IO.Path.Combine(directory, "unittest-device_2.png").Replace("\\", "/");
            System.IO.File.WriteAllBytes(first, new byte[] { 1, 2, 3 });

            try
            {
                var screenshots = captureScreenshot.GetScreenshots();
                Assert.IsTrue(screenshots.Any(s => s.Path == first), "The first file should be listed");
                Assert.IsFalse(screenshots.Any(s => s.Path == second), "The second one does not exist yet");

                // Written behind the cache's back, as a file browser would.
                System.IO.File.WriteAllBytes(second, new byte[] { 4, 5, 6 });

                Assert.IsFalse(captureScreenshot.GetScreenshots().Any(s => s.Path == second),
                    "A cached listing cannot know about a file the Editor did not write");

                captureScreenshot.InvalidateScreenshots();

                Assert.IsTrue(captureScreenshot.GetScreenshots().Any(s => s.Path == second),
                    "After invalidating, the rescan should pick the file up");

                // And the same for one that disappears.
                System.IO.File.Delete(first);
                Assert.IsTrue(captureScreenshot.GetScreenshots().Any(s => s.Path == first),
                    "Still cached, so still listed");

                captureScreenshot.InvalidateScreenshots();
                Assert.IsFalse(captureScreenshot.GetScreenshots().Any(s => s.Path == first),
                    "After invalidating, a file that is gone should be gone from the list");
            }
            finally
            {
                foreach (var path in new[] { first, second })
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }
            }
        }
        finally
        {
            runtime.Shutdown();
        }
    }

    [Test]
    public void SettingsRangeTests()
    {
        var range = new SettingsRange(70, 1, 100);
        Assert.AreEqual(70, range.Default);
        Assert.AreEqual(1, range.Min);
        Assert.AreEqual(100, range.Max);

        // A value the user chose is brought inside the bounds.
        Assert.AreEqual(1, range.Clamp(-5));
        Assert.AreEqual(1, range.Clamp(0));
        Assert.AreEqual(100, range.Clamp(1000));
        Assert.AreEqual(55, range.Clamp(55));
        Assert.AreEqual(1, range.Clamp(1));
        Assert.AreEqual(100, range.Clamp(100));

        // A value read back from an older settings blob falls back to the default
        // instead, since 0 there means nobody ever chose one.
        Assert.AreEqual(70, range.OrDefault(0));
        Assert.AreEqual(70, range.OrDefault(-5));
        Assert.AreEqual(70, range.OrDefault(1000));
        Assert.AreEqual(55, range.OrDefault(55));
        Assert.AreEqual(1, range.OrDefault(1));
        Assert.AreEqual(100, range.OrDefault(100));

        // A range that cannot hold its own default is a mistake at the declaration, so
        // it is refused rather than silently clamped.
        Assert.Throws(typeof(ArgumentException), () => new SettingsRange(0, 1, 100));
        Assert.Throws(typeof(ArgumentException), () => new SettingsRange(500, 1, 100));
        Assert.Throws(typeof(ArgumentException), () => new SettingsRange(5, 100, 1));
    }

    [Test]
    public void LiveStreamSettingsTests()
    {
        var settings = new AndroidLogcatSettings();

        // The defaults are what the live stream used to hold as constants.
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxSize.Default, settings.LiveStreamMaxSize);
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamQuality.Default, settings.LiveStreamQuality);
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxFps.Default, settings.LiveStreamMaxFps);

        // Clamped on the way in, so a hand edited settings file cannot hand the server
        // something it will refuse or choke on.
        settings.LiveStreamMaxSize = 1;
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxSize.Min, settings.LiveStreamMaxSize);
        settings.LiveStreamMaxSize = 100000;
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxSize.Max, settings.LiveStreamMaxSize);

        settings.LiveStreamQuality = 0;
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamQuality.Min, settings.LiveStreamQuality);
        settings.LiveStreamQuality = 1000;
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamQuality.Max, settings.LiveStreamQuality);

        settings.LiveStreamMaxFps = 0;
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxFps.Min, settings.LiveStreamMaxFps);
        settings.LiveStreamMaxFps = 1000;
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxFps.Max, settings.LiveStreamMaxFps);

        // A value inside the range is kept as it is.
        settings.LiveStreamMaxSize = 512;
        settings.LiveStreamQuality = 55;
        settings.LiveStreamMaxFps = 15;
        Assert.AreEqual(512, settings.LiveStreamMaxSize);
        Assert.AreEqual(55, settings.LiveStreamQuality);
        Assert.AreEqual(15, settings.LiveStreamMaxFps);

        // The section's own Reset button puts the three back without disturbing
        // anything else on the page.
        settings.MessageFontSize = 17;
        settings.MaxCachedMessageCount = 1234;
        settings.ResetLiveStreamSettings();
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxSize.Default, settings.LiveStreamMaxSize);
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamQuality.Default, settings.LiveStreamQuality);
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxFps.Default, settings.LiveStreamMaxFps);
        Assert.AreEqual(17, settings.MessageFontSize, "A live stream reset should not touch the font size");
        Assert.AreEqual(1234, settings.MaxCachedMessageCount, "A live stream reset should not touch the message cap");

        // And the whole page Reset takes them with everything else.
        settings.LiveStreamMaxSize = 512;
        settings.Reset();
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxSize.Default, settings.LiveStreamMaxSize);
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamQuality.Default, settings.LiveStreamQuality);
        Assert.AreEqual(AndroidLogcatSettings.kLiveStreamMaxFps.Default, settings.LiveStreamMaxFps);
    }

    [Test]
    public void LiveStreamEditingShortcutTests()
    {
        AndroidKeyCode mapped;

        // Ctrl on Windows and Linux, Cmd on macOS: both have to reach the device, where
        // they arrive as Ctrl either way.
        foreach (var modifier in new[] { EventModifiers.Control, EventModifiers.Command })
        {
            Assert.IsTrue(AndroidLogcatLiveStream.TryMapEditingShortcut(
                new Event { keyCode = KeyCode.A, modifiers = modifier }, out mapped), $"A with {modifier}");
            Assert.AreEqual(AndroidKeyCode.A, mapped);

            Assert.IsTrue(AndroidLogcatLiveStream.TryMapEditingShortcut(
                new Event { keyCode = KeyCode.C, modifiers = modifier }, out mapped), $"C with {modifier}");
            Assert.AreEqual(AndroidKeyCode.C, mapped);

            Assert.IsTrue(AndroidLogcatLiveStream.TryMapEditingShortcut(
                new Event { keyCode = KeyCode.V, modifiers = modifier }, out mapped), $"V with {modifier}");
            Assert.AreEqual(AndroidKeyCode.V, mapped);
        }

        // A bare letter is ordinary typing, which goes to the device as text instead.
        Assert.IsFalse(AndroidLogcatLiveStream.TryMapEditingShortcut(
            new Event { keyCode = KeyCode.A, modifiers = EventModifiers.None }, out mapped));

        // Every other Ctrl chord belongs to the Editor - Ctrl+S in particular.
        Assert.IsFalse(AndroidLogcatLiveStream.TryMapEditingShortcut(
            new Event { keyCode = KeyCode.S, modifiers = EventModifiers.Control }, out mapped));
        Assert.IsFalse(AndroidLogcatLiveStream.TryMapEditingShortcut(
            new Event { keyCode = KeyCode.Z, modifiers = EventModifiers.Control }, out mapped));

        // And so does anything with a further modifier on top, such as this window's own
        // Ctrl+Shift+S, so only the bare chord is taken.
        Assert.IsFalse(AndroidLogcatLiveStream.TryMapEditingShortcut(
            new Event { keyCode = KeyCode.A, modifiers = EventModifiers.Control | EventModifiers.Shift },
            out mapped));
        Assert.IsFalse(AndroidLogcatLiveStream.TryMapEditingShortcut(
            new Event { keyCode = KeyCode.V, modifiers = EventModifiers.Control | EventModifiers.Alt },
            out mapped));
    }

    [Test]
    public void ProjectRelativePathTests()
    {
        const string screenshot = "Library/AndroidLogcat/Screenshots/device_1.png";

        // Windows, where paths come in with backslashes and in whatever case the caller
        // happened to use - hence the case insensitive comparison in the function.
        var windows = "C:/Users/tomas/Projects/MyProject";
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(windows + "/" + screenshot, windows));
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(
                @"C:\Users\tomas\Projects\MyProject\Library\AndroidLogcat\Screenshots\device_1.png", windows));
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(
                @"c:\users\tomas\projects\myproject\Library\AndroidLogcat\Screenshots\device_1.png", windows));

        // macOS, and Linux with it: rooted at / with no drive, and project folders with
        // spaces in them are the norm rather than the exception.
        var osx = "/Users/tomas/Projects/MyProject";
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(osx + "/" + screenshot, osx));

        var osxWithSpaces = "/Users/tomas/Unity Projects/My Project";
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(osxWithSpaces + "/" + screenshot, osxWithSpaces));

        // A trailing slash on the project folder must not eat the first character of
        // what is left.
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(osx + "/" + screenshot, osx + "/"));

        // Outside the project there is nothing to strip.
        StringAssert.AreEqualIgnoringCase("/Users/tomas/Desktop/shot.png",
            AndroidLogcatUtilities.ProjectRelativePath("/Users/tomas/Desktop/shot.png", osx));
        StringAssert.AreEqualIgnoringCase("D:/elsewhere/shot.png",
            AndroidLogcatUtilities.ProjectRelativePath("D:/elsewhere/shot.png", windows));

        // A folder whose name merely starts with the project folder's must not be taken
        // for something inside it.
        StringAssert.AreEqualIgnoringCase(osx + "2/shot.png",
            AndroidLogcatUtilities.ProjectRelativePath(osx + "2/shot.png", osx));

        // The project folder itself is not a file in the project, so it is left alone
        // rather than turned into an empty string.
        StringAssert.AreEqualIgnoringCase(osx, AndroidLogcatUtilities.ProjectRelativePath(osx, osx));

        // And through the public entry point, which is what the screenshot list calls,
        // to prove it is wired to this project's folder.
        var project = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")).Replace("\\", "/");
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(project + "/" + screenshot));

        Assert.AreEqual(string.Empty, AndroidLogcatUtilities.ProjectRelativePath(string.Empty));
        Assert.IsNull(AndroidLogcatUtilities.ProjectRelativePath(null));
    }

    [Test]
    public void ParseVersionTests()
    {
        var values = new KeyValuePair<Version, string>[]
        {
            new KeyValuePair<Version, string>(new Version(1, 0), "1"),
            new KeyValuePair<Version, string>(new Version(1, 2), "1.2"),
            new KeyValuePair<Version, string>(new Version(1, 2, 3), "1.2.3"),
            new KeyValuePair<Version, string>(new Version(1, 2, 3, 4), "1.2.3.4")
        };

        foreach (var v in values)
        {
            Assert.AreEqual(v.Key, AndroidLogcatUtilities.ParseVersionLegacy(v.Value));
            Assert.AreEqual(v.Key, AndroidLogcatUtilities.ParseVersion(v.Value));
        }
    }

    /// <summary>
    /// The zoom of the live view and the screenshot preview. No device and no GUI: what
    /// the wheel and the drag do to the view is arithmetic that can simply be called.
    /// </summary>
    [Test]
    public void ImageViewerZoomsBetween100And4000Percent()
    {
        var viewer = new AndroidLogcatImageViewer();
        var area = new Rect(0, 0, 400, 300);
        // The area's own shape, so the image fills it at 100%.
        const float aspect = 4.0f / 3.0f;

        Assert.AreEqual(AndroidLogcatImageViewer.kMinZoom, viewer.Zoom, 0.0001f,
            "Expected to start at 100%");
        Assert.IsFalse(viewer.IsZoomed);

        // A notch of the wheel is a delta of 3, and scrolling up reports it negative.
        Assert.IsTrue(viewer.ZoomAt(area, aspect, area.center, -3.0f), "Expected one notch to zoom in");
        Assert.Greater(viewer.Zoom, AndroidLogcatImageViewer.kMinZoom);
        Assert.IsTrue(viewer.IsZoomed);

        for (var notch = 0; notch < 100; notch++)
            viewer.ZoomAt(area, aspect, area.center, -3.0f);

        Assert.AreEqual(AndroidLogcatImageViewer.kMaxZoom, viewer.Zoom, 0.0001f,
            "Expected to stop at 4000%");
        Assert.IsFalse(viewer.ZoomAt(area, aspect, area.center, -3.0f),
            "Expected no change once the zoom is at its maximum");

        for (var notch = 0; notch < 100; notch++)
            viewer.ZoomAt(area, aspect, area.center, 3.0f);

        Assert.AreEqual(AndroidLogcatImageViewer.kMinZoom, viewer.Zoom, 0.0001f,
            "Expected to stop at 100%");
        Assert.IsFalse(viewer.ZoomAt(area, aspect, area.center, 3.0f),
            "Expected no change once the zoom is at its minimum");
        Assert.IsFalse(viewer.IsZoomed);
        Assert.AreEqual(Vector2.zero, viewer.Scroll,
            "Zooming all the way back out should leave nothing scrolled out of view");
    }

    [Test]
    public void ImageViewerZoomKeepsWhatIsUnderTheCursorThere()
    {
        var viewer = new AndroidLogcatImageViewer();
        var area = new Rect(0, 0, 400, 300);
        const float aspect = 4.0f / 3.0f;
        // The quarter point of the area, and so of the image in it.
        var pointer = new Vector2(100, 75);

        // Four notches double the zoom, so twelve wheel units is exactly 200%.
        Assert.IsTrue(viewer.ZoomAt(area, aspect, pointer, -12.0f));
        Assert.AreEqual(2.0f, viewer.Zoom, 0.0001f);

        // (100,75) of the 400x300 area is (200,150) of the 800x600 it has become, and
        // that has to end up back under the cursor - so the view scrolls by (100,75).
        Assert.AreEqual(100.0f, viewer.Scroll.x, 0.001f);
        Assert.AreEqual(75.0f, viewer.Scroll.y, 0.001f);

        viewer.Reset();
        Assert.AreEqual(AndroidLogcatImageViewer.kMinZoom, viewer.Zoom, 0.0001f);
        Assert.AreEqual(Vector2.zero, viewer.Scroll);
    }

    [Test]
    public void ImageViewerPansOnlyWithinTheZoomedImage()
    {
        var viewer = new AndroidLogcatImageViewer();
        var area = new Rect(0, 0, 400, 300);
        const float aspect = 4.0f / 3.0f;

        // Nothing to move at 100%: the image is exactly the area.
        viewer.Pan(area, aspect, new Vector2(-50, -50));
        Assert.AreEqual(Vector2.zero, viewer.Scroll);

        viewer.ZoomAt(area, aspect, area.min, -12.0f);
        Assert.AreEqual(Vector2.zero, viewer.Scroll,
            "Zooming in on the top left corner should have nothing scrolled out of view yet");

        // Far past the end of the image: the far corner has to be reachable, and the
        // image must not carry on off the view.
        viewer.Pan(area, aspect, new Vector2(-10000, -10000));
        Assert.GreaterOrEqual(viewer.Scroll.x, area.width * (viewer.Zoom - 1.0f),
            "Expected to be able to reach the right edge of the image");
        Assert.GreaterOrEqual(viewer.Scroll.y, area.height * (viewer.Zoom - 1.0f),
            "Expected to be able to reach the bottom edge of the image");
        Assert.Less(viewer.Scroll.x, area.width * viewer.Zoom,
            "Expected not to be able to drag the image out of the view");
        Assert.Less(viewer.Scroll.y, area.height * viewer.Zoom,
            "Expected not to be able to drag the image out of the view");

        // And back, which stops at the near corner rather than going past it.
        viewer.Pan(area, aspect, new Vector2(10000, 10000));
        Assert.AreEqual(Vector2.zero, viewer.Scroll);
    }

    [Test]
    public void ParsePIDNameTests()
    {
        // Produced by adb.exe shell ps -p 816
        var android90Output1 = @"
USER           PID  PPID     VSZ    RSS WCHAN            ADDR S NAME
system         816     1   24396   2296 0                   0 S /test/thermal-daemon
";
        // Produced by adb.exe shell ps -p 816 -o NAME
        var android90Output2 = @"
USER           PID  PPID     VSZ    RSS WCHAN            ADDR S NAME
system         816     1   24396   2296 0                   0 S /test/thermal-daemon
";

        // Produced by adb.exe -s shell ps -p 279
        // Note: -o NAME doesn't work on Android 5.0
        var android50Output = @"
USER     PID   PPID  VSIZE  RSS   PRIO  NICE  RTPRI SCHED   WCHAN    PC        NAME
root      279   1     24908  908   20    0     0     0     ffffffff 00000000 S /system/bin/netd
";

        Assert.AreEqual("/test/thermal-daemon", AndroidLogcatUtilities.ProcessOutputFromPS(android90Output1));
        Assert.AreEqual("/test/thermal-daemon", AndroidLogcatUtilities.ProcessOutputFromPS(android90Output2));
        Assert.AreEqual("/system/bin/netd", AndroidLogcatUtilities.ProcessOutputFromPS(android50Output));
    }

    [Test]
    public void CanSetGetTagPriorities()
    {
        var device = new AndroidLogcatFakeDevice90("Fake90");
        var builtinTags = AndroidLogcatTags.DefaultTagNames;
        foreach (var b in builtinTags)
        {
            Assert.AreEqual(Priority.Verbose, device.GetTagPriority(b));

            device.SetTagPriority(b, Priority.Error);
            Assert.AreEqual(Priority.Error, device.GetTagPriority(b));
        }
    }
}
