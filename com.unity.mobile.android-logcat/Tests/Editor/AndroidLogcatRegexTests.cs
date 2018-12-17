using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Unity.Android.Logcat;

class AndroidLogcatRegexTests
{
    // Messages produced via adb logcat -s -v threadtime *:V
    private string[] kLogMessagesWithThreadTimeFormat = new[]
    {
        "10-25 14:27:29.803  1277 10543 E ctxmgr  : [AccountAclCallback]Failed Acl fetch: network status=-1",
        "10-25 14:27:43.785  2255  2642 I chromium: [2255:2642:INFO: mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 32 seconds: 13",
        "10-25 14:27:56.862  2255  2255 I chromium: [2255:2255:INFO:metrics_recorder.cc(89)] Metrics stat: total=8",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.AppId.In=3",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.In=13",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.Namespace.In=11",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.ResponderPing=1",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.AppId.Out=3",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Error.NamespaceNotSupported=11",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Out=13",
        "10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.SocketPing=2",
        "10-25 14:28:10.312  2255  2642 I chromium: [2255:2642:INFO:mdns_cast_service.cc(755)] Recent mDNS app subtypes: [supported:'805741C9',] [unsupported:]",
        "10-25 14:28:16.994  2255  2642 I chromium: [2255:2642:INFO:mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 33 seconds: 8",
        // Add more as needed
    };

    // Note: -v year is not available on Android 6.0 and below
    // Messages produced via adb logcat -s -v year *:V
    private string[] kLogMessagesWithYearFormat = new[]
    {
        "2018-10-25 14:27:29.803  1277 10543 E ctxmgr  : [AccountAclCallback]Failed Acl fetch: network status=-1",
        "2018-10-25 14:27:43.785  2255  2642 I chromium: [2255:2642:INFO: mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 32 seconds: 13",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: [2255:2255:INFO:metrics_recorder.cc(89)] Metrics stat: total=8",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.AppId.In=3",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.In=13",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.Namespace.In=11",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.ResponderPing=1",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.AppId.Out=3",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Error.NamespaceNotSupported=11",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Out=13",
        "2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.SocketPing=2",
        "2018-10-25 14:28:10.312  2255  2642 I chromium: [2255:2642:INFO:mdns_cast_service.cc(755)] Recent mDNS app subtypes: [supported:'805741C9',] [unsupported:]",
        "2018-10-25 14:28:16.994  2255  2642 I chromium: [2255:2642:INFO:mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 33 seconds: 8",
        // Add more as needed
    };
    [Test]
    public void LogMessageRegexMatchesForThreadTimeFormat()
    {
        foreach (var l in kLogMessagesWithThreadTimeFormat)
        {
            Assert.IsTrue(AndroidLogcat.m_LogCatEntryThreadTimeRegex.IsMatch(l));
        }
    }

    [Test]
    public void LogMessageRegexMatchesForYearFormat()
    {
        foreach (var l in kLogMessagesWithYearFormat)
        {
            Assert.IsTrue(AndroidLogcat.m_LogCatEntryYearRegex.IsMatch(l));
        }
    }



    [Test]
    public void CorrectlyParsePIDsWithWindowsEndlines()
    {
        CorrectlyParsePIDs("\r\n");
    }

    [Test]
    public void CorrectlyParsePIDsWithUnixEndlines()
    {
        CorrectlyParsePIDs("\n");
    }

    private void CorrectlyParsePIDs(string separator)
    {
        var expectedPid = 2909;
        var adbContents = string.Join(separator, new[]
        {
            "USER      PID   PPID  VSIZE  RSS   WCHAN              PC  NAME",
"root      1     0     29876  1192  SyS_epoll_ 0000000000 S /init",
"root      2     0     0      0       kthreadd 0000000000 S kthreadd",
"root      3     2     0      0     smpboot_th 0000000000 S ksoftirqd/0",
"root      4     2     0      0     worker_thr 0000000000 S kworker/0:0",
"system    " + expectedPid + "  885   2415772 149576 SyS_epoll_ 0000000000 S com.android.settings",
"system    " + (expectedPid + 1) +"  885   2415772 149576 SyS_epoll_ 0000000000 S com.android.settings", // This should never happen - two packages but different process id, but let's have it anyways
"system    3092  885   1824084 76696 SyS_epoll_ 0000000000 S com.sec.epdg",
"u0_a196   6964  885   1819464 74992 SyS_epoll_ 0000000000 S com.samsung.android.asksmanager",
"system    6977  885   1820400 77772 SyS_epoll_ 0000000000 S com.samsung.android.bbc.bbcagent",
"bcmgr     6991  885   1823868 76620 SyS_epoll_ 0000000000 S com.samsung.android.beaconmanager",
"u0_a190   7004  885   1824168 71740 SyS_epoll_ 0000000000 S com.samsung.android.bluelightfilter",
"u0_a219   7026  885   1849988 83532 SyS_epoll_ 0000000000 S com.samsung.android.calendar",
"shell     7045  5404  9864   4124           0 7f7aa45738 R ps"
        });

        var pid = AndroidLogcatConsoleWindow.ParsePIDInfo("com.android.settings", adbContents);
        
        Assert.IsTrue(pid == expectedPid, "Process Id has to be " + expectedPid + ", but was " + pid);

        pid = AndroidLogcatConsoleWindow.ParsePIDInfo("com.I.DontExist", adbContents);
        Assert.IsTrue(pid == -1, "Process Id has to be -1 , but was " + pid);


        var invalidAdbContents = "blabla";
        pid = AndroidLogcatConsoleWindow.ParsePIDInfo("com.I.DontExist", invalidAdbContents);
        Assert.IsTrue(pid == -1, "Process Id has to be -1 , but was " + pid);
    }
}
