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
}
