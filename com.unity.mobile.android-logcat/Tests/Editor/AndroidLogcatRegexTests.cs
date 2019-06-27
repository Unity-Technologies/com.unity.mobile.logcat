using NUnit.Framework;
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
        "01-18 14:14:56.254  3777  6386 I BarTender:BatteryStatsDumper: writing to daily db completed",
        "01-19 22:21:51.151  1461  5286 D SSRM:k  : SIOP:: AP = 160, PST = 160 (W:14), CP = 18, CUR = 398, LCD = 57",
        "01-19 14:58:16.725  3966  3966 D u       : getCurrentNetTypeId, current net type: null",
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
        "2019-01-18 14:14:56.254  3777  6386 I BarTender:BatteryStatsDumper: writing to daily db completed",
        "2019-01-18 22:21:51.151  1461  5286 D SSRM:k  : SIOP:: AP = 160, PST = 160 (W:14), CP = 18, CUR = 398, LCD = 57",
        "2019-01-18 14:58:16.725  3966  3966 D u       : getCurrentNetTypeId, current net type: null",
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
}
