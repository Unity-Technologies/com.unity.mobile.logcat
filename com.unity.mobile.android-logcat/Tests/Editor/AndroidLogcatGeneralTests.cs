using System;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Android.Logcat;

class AndroidLogcatGeneralTests
{
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
        const string screenshot = "UserSettings/AndroidLogcat/Screenshots/device_1.png";

        // Windows, where paths come in with backslashes and in whatever case the caller
        // happened to use - hence the case insensitive comparison in the function.
        var windows = "C:/Users/tomas/Projects/MyProject";
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(windows + "/" + screenshot, windows));
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(
                @"C:\Users\tomas\Projects\MyProject\UserSettings\AndroidLogcat\Screenshots\device_1.png", windows));
        StringAssert.AreEqualIgnoringCase(screenshot,
            AndroidLogcatUtilities.ProjectRelativePath(
                @"c:\users\tomas\projects\myproject\UserSettings\AndroidLogcat\Screenshots\device_1.png", windows));

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
