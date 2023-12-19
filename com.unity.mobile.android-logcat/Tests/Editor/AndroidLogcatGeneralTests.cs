using System;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Android.Logcat;

public class AndroidLogcatGeneralTests
{
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
