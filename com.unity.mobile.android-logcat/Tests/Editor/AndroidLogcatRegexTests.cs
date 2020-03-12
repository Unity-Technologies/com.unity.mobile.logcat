using NUnit.Framework;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;

class AndroidLogcatRegexTests
{
    struct LogcatMessage
    {
        string fullMessage;
        string expectedTag;

        internal string FullMessage { get { return fullMessage; } }
        internal string ExpectedTag { get { return expectedTag; } }

        internal LogcatMessage(string _fullMessage, string _expectedTag)
        {
            fullMessage = _fullMessage;
            expectedTag = _expectedTag;
        }
    }

    // Messages produced via adb logcat -s -v threadtime *:V
    private LogcatMessage[] kLogMessagesWithThreadTimeFormat = new[]
    {
        new LogcatMessage("10-25 14:27:29.803  1277 10543 E ctxmgr  : [AccountAclCallback]Failed Acl fetch: network status=-1", "ctxmgr"),
        new LogcatMessage("10-25 14:27:43.785  2255  2642 I chromium: [2255:2642:INFO: mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 32 seconds: 13", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: [2255:2255:INFO:metrics_recorder.cc(89)] Metrics stat: total=8", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.AppId.In=3", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.In=13", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.Namespace.In=11", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.ResponderPing=1", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.AppId.Out=3", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Error.NamespaceNotSupported=11", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Out=13", "chromium"),
        new LogcatMessage("10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.SocketPing=2", "chromium"),
        new LogcatMessage("10-25 14:28:10.312  2255  2642 I chromium: [2255:2642:INFO:mdns_cast_service.cc(755)] Recent mDNS app subtypes: [supported:'805741C9',] [unsupported:]", "chromium"),
        new LogcatMessage("10-25 14:28:16.994  2255  2642 I chromium: [2255:2642:INFO:mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 33 seconds: 8", "chromium"),
        new LogcatMessage("01-18 14:14:56.254  3777  6386 I BarTender:BatteryStatsDumper: writing to daily db completed", "BarTender:BatteryStatsDumper"),
        new LogcatMessage("01-19 22:21:51.151  1461  5286 D SSRM:k  : SIOP:: AP = 160, PST = 160 (W:14), CP = 18, CUR = 398, LCD = 57", "SSRM:k"),
        new LogcatMessage("01-19 14:58:16.725  3966  3966 D u       : getCurrentNetTypeId, current net type: null", "u"),
        new LogcatMessage("01-19 14:58:16.725  3966  3966 D EPDG -- SIM0 [EpdgSubScription]: getCurrentNetTypeId, current net type: null", "EPDG -- SIM0 [EpdgSubScription]"),
        new LogcatMessage("03-10 14:33:13.505 11287 11287 D Unity   : NewInput[0xFFFFFFFFEA4E9DC0]: Incoming event with sources 'Touchscreen' from android device 3, isGameController: No, unity devices: 4", "Unity"),
        new LogcatMessage("03-10 14:33:13.505 11287 11287 D Unity   :     NewInput[0xFFFFFFFFEA4E9DC0]: Touch 1454.000000 x 343.000000, touchId = 6 (0), phase = kEnded, time = 125.525207 (594002743)", "Unity")
        // Add more as needed
    };


    // Note: -v year is not available on Android 6.0 and below
    // Messages produced via adb logcat -s -v year *:V
    private LogcatMessage[] kLogMessagesWithYearFormat = new[]
    {
        new LogcatMessage("2018-10-25 14:27:29.803  1277 10543 E ctxmgr  : [AccountAclCallback]Failed Acl fetch: network status=-1", "ctxmgr"),
        new LogcatMessage("2018-10-25 14:27:43.785  2255  2642 I chromium: [2255:2642:INFO: mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 32 seconds: 13", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: [2255:2255:INFO:metrics_recorder.cc(89)] Metrics stat: total=8", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.AppId.In=3", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.In=13", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Request.Namespace.In=11", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.ResponderPing=1", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.AppId.Out=3", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Error.NamespaceNotSupported=11", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.Response.Out=13", "chromium"),
        new LogcatMessage("2018-10-25 14:27:56.862  2255  2255 I chromium: Cast.Discovery.Mdns.SocketPing=2", "chromium"),
        new LogcatMessage("2018-10-25 14:28:10.312  2255  2642 I chromium: [2255:2642:INFO:mdns_cast_service.cc(755)] Recent mDNS app subtypes: [supported:'805741C9',] [unsupported:]", "chromium"),
        new LogcatMessage("2018-10-25 14:28:16.994  2255  2642 I chromium: [2255:2642:INFO:mdns_app_filter.cc(2202)] MdnsAppFilter: responses sent in 33 seconds: 8", "chromium"),
        new LogcatMessage("2019-01-18 14:14:56.254  3777  6386 I BarTender:BatteryStatsDumper: writing to daily db completed", "BarTender:BatteryStatsDumper"),
        new LogcatMessage("2019-01-18 22:21:51.151  1461  5286 D SSRM:k  : SIOP:: AP = 160, PST = 160 (W:14), CP = 18, CUR = 398, LCD = 57", "SSRM:k"),
        new LogcatMessage("2019-01-18 14:58:16.725  3966  3966 D u       : getCurrentNetTypeId, current net type: null", "u"),
        new LogcatMessage("2020-02-06 12:48:19.406  2579  2813 D EPDG -- SIM0 [EpdgSubScription]: getMnoNameFromDB() hassim :true", "EPDG -- SIM0 [EpdgSubScription]"),
        // Add more as needed
    };
    [Test]
    public void LogMessageRegexMatchesForThreadTimeFormat()
    {
        foreach (var l in kLogMessagesWithThreadTimeFormat)
        {
            var result = AndroidLogcat.m_LogCatEntryThreadTimeRegex.Match(l.FullMessage);
            Assert.IsTrue(result.Success);
            var tagValue = result.Groups["tag"].Value;
            Assert.AreEqual(l.ExpectedTag, tagValue);
        }
    }

    [Test]
    public void LogMessageRegexMatchesForYearFormat()
    {
        foreach (var l in kLogMessagesWithYearFormat)
        {
            var result = AndroidLogcat.m_LogCatEntryYearRegex.Match(l.FullMessage);
            Assert.IsTrue(result.Success, l.FullMessage);
            var tagValue = result.Groups["tag"].Value;
            Assert.AreEqual(l.ExpectedTag, tagValue);
        }
    }

    [Test]
    public void WhitespacesArePreservedInMessage()
    {
        var myTestMessage = "  hello  ";
        var msgWithYearFormat = @"2018-10-25 14:27:29.803  1277 10543 E ctxmgr  : " + myTestMessage;
        var msgWithThreadTimeFormat = "10-25 14:27:29.803  1277 10543 E ctxmgr  : " + myTestMessage;

        var yearMsg = AndroidLogcat.m_LogCatEntryYearRegex.Match(msgWithYearFormat).Groups["msg"].Value;
        Assert.IsTrue(yearMsg.Equals(myTestMessage));

        var threadMsg = AndroidLogcat.m_LogCatEntryThreadTimeRegex.Match(msgWithThreadTimeFormat).Groups["msg"].Value;
        Assert.IsTrue(threadMsg.Equals(myTestMessage));
    }

    [Test]
    public void CorrectlyParsePidsWithWindowsEndlines()
    {
        CorrectlyParsePids("\r\n");
    }

    [Test]
    public void CorrectlyParsePidsWithUnixEndlines()
    {
        CorrectlyParsePids("\n");
    }

    private void CorrectlyParsePids(string separator)
    {
        var expectedPid = 2909;
        // Produced by adb shell ps
        var adbContents = string.Join(separator, new[]
        {
            "USER      PID   PPID  VSIZE  RSS   WCHAN              PC  NAME",
            "root      1     0     29876  1192  SyS_epoll_ 0000000000 S /init",
            "root      2     0     0      0       kthreadd 0000000000 S kthreadd",
            "root      3     2     0      0     smpboot_th 0000000000 S ksoftirqd/0",
            "root      4     2     0      0     worker_thr 0000000000 S kworker/0:0",
            "system    " + expectedPid + "  885   2415772 149576 SyS_epoll_ 0000000000 S com.android.settings",
            "system    " + (expectedPid + 1) + "  885   2415772 149576 SyS_epoll_ 0000000000 S com.android.settings", // This should never happen - two packages but different process id, but let's have it anyways
            "system    3092  885   1824084 76696 SyS_epoll_ 0000000000 S com.sec.epdg",
            "u0_a196   6964  885   1819464 74992 SyS_epoll_ 0000000000 S com.samsung.android.asksmanager",
            "system    6977  885   1820400 77772 SyS_epoll_ 0000000000 S com.samsung.android.bbc.bbcagent",
            "bcmgr     6991  885   1823868 76620 SyS_epoll_ 0000000000 S com.samsung.android.beaconmanager",
            "u0_a190   7004  885   1824168 71740 SyS_epoll_ 0000000000 S com.samsung.android.bluelightfilter",
            "u0_a219   7026  885   1849988 83532 SyS_epoll_ 0000000000 S com.samsung.android.calendar",
            "shell     7045  5404  9864   4124           0 7f7aa45738 R ps"
        });

        var pid = AndroidLogcatUtilities.ParsePidInfo("com.android.settings", adbContents);

        Assert.IsTrue(pid == expectedPid, "Process Id has to be " + expectedPid + ", but was " + pid);

        pid = AndroidLogcatUtilities.ParsePidInfo("com.I.DontExist", adbContents);
        Assert.IsTrue(pid == -1, "Process Id has to be -1 , but was " + pid);


        var invalidAdbContents = "blabla";
        pid = AndroidLogcatUtilities.ParsePidInfo("com.I.DontExist", invalidAdbContents);
        Assert.IsTrue(pid == -1, "Process Id has to be -1 , but was " + pid);
    }

    [Test]
    public void CorrectlyParseTopActivityWithWindowsEndlines()
    {
        CorrectlyParseTopActivit("\r\n");
    }

    [Test]
    public void CorrectlyParseTopActivityWithUnixEndlines()
    {
        CorrectlyParseTopActivit("\n");
    }

    private void CorrectlyParseTopActivit(string separator)
    {
        // Produced by adb shell dumpsys activity
        var adbContents = string.Join(separator, new[]
        {
            "ACTIVITY MANAGER PENDING INTENTS (dumpsys activity intents)",
            "  * PendingIntentRecord{28bcd62 com.google.android.partnersetup startService}",
            "",
            "  Process LRU list (sorted by oom_adj, 84 total, non-act at 2, non-svc at 2):",
            "    PERS #83: sys   F/ /P  trm: 0 1672:system/1000 (fixed)",
            "    PERS #82: pers  F/ /P  trm: 0 2560:com.android.phone/1001 (fixed)",
            "    PERS #81: pers  F/ /P  trm: 0 2589:com.android.systemui/u0a62 (fixed)",
            "    PERS #80: pers  F/ /P  trm: 0 2801:com.sec.imsservice/1000 (fixed)",
            "    PERS #79: pers  F/ /P  trm: 0 3092:com.sec.epdg/1000 (fixed)",
            "    PERS #77: pers  F/ /P  trm: 0 3160:com.sec.sve/1000 (fixed)",
            "    PERS #75: pers  F/ /P  trm: 0 3540:com.sec.android.app.wfdbroker/1000 (fixed)",
            "    PERS #74: pers  F/ /P  trm: 0 3553:com.samsung.android.radiobasedlocation/1000 (fixed)",
            "    PERS #73: pers  F/ /P  trm: 0 3577:com.android.nfc/1027 (fixed)",
            "    PERS #72: pers  F/ /P  trm: 0 3588:com.samsung.android.providers.context/u0a8 (fixed)",
            "    PERS #71: pers  F/ /P  trm: 0 3610:system/u0a82 (fixed)",
            "    PERS #70: pers  F/ /P  trm: 0 3618:com.samsung.vzwapiservice/1000 (fixed)",
            "    PERS #69: pers  F/ /P  trm: 0 3636:com.qualcomm.qti.services.secureui:sui_service/1000 (fixed)",
            "    PERS #68: pers  F/ /P  trm: 0 3658:com.sec.enterprise.knox.shareddevice.keyguard/1000 (fixed)",
            "    Proc #55: psvc  F/ /IF trm: 0 2573:com.android.bluetooth/1002 (service)",
            "        com.android.bluetooth/.pbap.BluetoothPbapService<=Proc{2589:com.android.systemui/u0a62}",
            "    Proc # 0: fore  R/A/T  trm: 0 3766:com.sec.android.app.launcher/u0a65 (top-activity)",
            "    Proc #76: vis   F/ /SB trm: 0 3239:com.google.android.ext.services/u0a194 (service)",
            "        com.google.android.ext.services/android.ext.services.notification.Ranker<=Proc{1672:system/1000}",
            "    Proc #67: vis   F/ /SB trm: 0 3532:com.google.android.googlequicksearchbox:interactor/u0a70 (service)",
            "        {null}<=android.os.BinderProxy@9243680",
            "    Proc #59: prcp  F/ /IF trm: 0 7295:com.samsung.android.oneconnect:QcService/u0a212 (force-fg)",
            "ACTIVITY MANAGER LOCALE CHANGED HISTORY",
            " (nothing) "
        });

        string packageName;
        var pid = AndroidLogcatUtilities.ParseTopActivityPackageInfo(adbContents, out packageName);

        var expectedPid = 3766;
        var expectedPackage = "com.sec.android.app.launcher";
        Assert.IsTrue(pid == 3766, "Expected top activity process id to be " + expectedPid + ", but was " + pid);
        Assert.IsTrue(packageName == expectedPackage, "Expected top activity package to be " + expectedPackage + ", but was " + packageName);

        var invalidAdbContents = "blabla";
        pid = AndroidLogcatUtilities.ParseTopActivityPackageInfo(invalidAdbContents, out packageName);
        Assert.IsTrue(pid == -1, "Expected top activity process id to be -1 but was " + pid);
        Assert.IsTrue(packageName == "", "Expected top activity package to be empty, but was " + packageName);
    }

    [Test]
    public void ParseCrashStackrace()
    {
        var regex = new Regex(AndroidLogcatStacktraceWindow.m_DefaultAddressRegex);

        string crash32 = "2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 002983fc  /data/app/com.mygame==/lib/arm/libunity.so";
        string crash64 = "2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 002983fc002983fc  /data/app/com.mygame==/lib/arm/libunity.so";

        var result = regex.Match(crash32);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(result.Groups[1].Value, "002983fc");
        Assert.AreEqual(result.Groups[2].Value, "libunity.so");

        result = regex.Match(crash64);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(result.Groups[1].Value, "002983fc002983fc");
        Assert.AreEqual(result.Groups[2].Value, "libunity.so");
    }

    [Test]
    public void ParseBuildInfo()
    {
        var buildType = "Release";
        var cpu = "armeabi-v7a";
        var backend = "mono";
        var buildInfos = new[]
        {
            "Built from '2019.3/staging' branch, Version '2019.3.0f5 (44796c9d3c2c)', Build type '" + buildType + "', Scripting Backend '" + backend + "', CPU '" + cpu + "', Stripping 'Disabled'",
            "Built from '2019.3/staging' branch, Version '2019.3.0f5 (44796c9d3c2c)', Build type '" + buildType + "', Scripting Backend '" + backend + "', CPU '" + cpu + "'"
        };
        foreach (var b in buildInfos)
        {
            var buildInfo = AndroidLogcatUtilities.ParseBuildInfo(b);
            UnityEngine.Debug.Log("Parsing:\n" + b);
            Assert.AreEqual(buildInfo.buildType, buildType.ToLower());
            Assert.AreEqual(buildInfo.cpu, cpu);
            Assert.AreEqual(buildInfo.scriptingImplementation, backend);
        }
    }
}
