using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Android.Logcat;
using System.Linq;
using System.Text;

class AndroidLogcatPacakgeInfoTests
{
    private void AssertDictionary(Dictionary<string, string> dictionary, string key, string value)
    {
        var allKeys = string.Join("\n", dictionary.Keys);
        Assert.IsTrue(dictionary.ContainsKey(key), $"Key {key} doesn't exist, existing keys:\n{allKeys}");

        Assert.IsTrue(dictionary[key].Equals(value), $"Key = {key}, expected value '{value}', but got '{dictionary[key]}'");
    }

    private Dictionary<string, string> EntriesToDictionary(List<KeyValuePair<string, string>> entries)
    {
        Assert.That(entries.Count, Is.GreaterThan(0));

        var failedParsings = new StringBuilder();
        foreach (var e in entries)
        {
            if (e.Key.Equals(AndroidLogcatPackageInfoParser.FailedKey))
                failedParsings.AppendLine(e.Value);
        }

        Assert.IsTrue(failedParsings.Length == 0, $"Failed to parse some values:\n{failedParsings}");

        Dictionary<string, string> dictionaryEntries;
        try
        {
            dictionaryEntries = entries.ToDictionary(x => x.Key, x => x.Value);
        }
        catch (Exception ex)
        {
            var values = new StringBuilder();
            foreach (var e in entries)
            {
                values.AppendLine($"{e.Key} = {e.Value}");
            }

            throw new Exception($"Failed to form dictionary from:\n{values}", ex);
        }
        return dictionaryEntries;
    }

    // adb shell dumpsys package <package information>
    [Test]
    public void CanParsePackageInfo()
    {
        var packageName = "com.DefaultCompany.AndroidEmptyGameActivity";
        var contents = @$"  Some other data

  Package [{packageName}] (ffc5f27):
    userId=10198
    pkg=Package{{d78b8ca {packageName}}}
    codePath=/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/{packageName}-bzIOds7vr6jRoCaEw1TacQ==
    resourcePath=/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/{packageName}-bzIOds7vr6jRoCaEw1TacQ==
    legacyNativeLibraryDir=/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/{packageName}-bzIOds7vr6jRoCaEw1TacQ==/lib
    primaryCpuAbi=armeabi-v7a
    secondaryCpuAbi=null
    versionCode=1 minSdk=22 targetSdk=32
    versionName=0.1
    splits=[base]
    apkSigningVersion=2
    applicationInfo=ApplicationInfo{{d78b8ca {packageName}}}
    flags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA TEST_ONLY ALLOW_BACKUP ]
    privateFlags=[ PRIVATE_FLAG_ACTIVITIES_RESIZE_MODE_RESIZEABLE_VIA_SDK_VERSION ALLOW_AUDIO_PLAYBACK_CAPTURE PRIVATE_FLAG_ALLOW_NATIVE_HEAP_POINTER_TAGGING ]
    forceQueryable=false
    queriesPackages=[]
    dataDir=/data/user/0/{packageName}
    fakeEmptyEntry=
    supportsScreens=[small, medium, large, xlarge, resizeable, anyDensity]
    timeStamp=2022-09-19 15:52:25
    firstInstallTime=2022-09-13 16:14:57
    lastUpdateTime=2022-09-19 15:52:26
    signatures=PackageSignatures{{af06a3b version:2, signatures:[ca97cd1f], past signatures:[]}}
    installPermissionsFixed=true
    pkgFlags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA TEST_ONLY ALLOW_BACKUP ]
    requested permissions:
      android.permission.INTERNET
      android.permission.ACCESS_NETWORK_STATE
    install permissions:
      android.permission.INTERNET: granted=true
      android.permission.ACCESS_NETWORK_STATE: granted=true
    User 0: ceDataInode=311793 installed=true hidden=false suspended=false distractionFlags=0 stopped=false notLaunched=false enabled=0 instant=false virtual=false
      gids=[3003]
      runtime permissions:
";
        // Note: User 0 parsing looks ugly and incorrect
        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = EntriesToDictionary(parser.ParsePackageInformationAsPairs(packageName));

        AssertDictionary(entries, "pkg", $"Package{{d78b8ca {packageName}}}");
        AssertDictionary(entries, "codePath", $"/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/{packageName}-bzIOds7vr6jRoCaEw1TacQ==");
        AssertDictionary(entries, "flags", "[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA TEST_ONLY ALLOW_BACKUP ]");
        AssertDictionary(entries, "firstInstallTime", $"2022-09-13 16:14:57");
        AssertDictionary(entries, "fakeEmptyEntry", string.Empty);
        AssertDictionary(entries, "signatures", $"PackageSignatures{{af06a3b version:2, signatures:[ca97cd1f], past signatures:[]}}");
    }

    [Test]
    public void CanParseEmpty()
    {
        var packageName = "com.dummy.dummy";
        var contents = @$"
  Package [{packageName}] (ffc5f27):";
        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = parser.ParsePackageInformationAsPairs(packageName);
        Assert.AreEqual(0, entries.Count);
    }

    [Test]
    public void CanParsePermissionsWithEndLine()
    {
        var packageName = "com.dummy.dummy";
        var contents = @$"
  Package [{packageName}] (ffc5f27):
    requested permissions:
      android.permission.INTERNET
      android.permission.ACCESS_NETWORK_STATE";

        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = EntriesToDictionary(parser.ParsePackageInformationAsPairs(packageName));

        AssertDictionary(entries, "requested permissions:", $"android.permission.INTERNET, android.permission.ACCESS_NETWORK_STATE");
    }

    [Test]
    public void CanParsePermissionsWithExtraLine()
    {
        var packageName = "com.dummy.dummy";
        var contents = @$"
  Package [{packageName}] (ffc5f27):
    requested permissions:
      android.permission.INTERNET
      android.permission.ACCESS_NETWORK_STATE
";
        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = EntriesToDictionary(parser.ParsePackageInformationAsPairs(packageName));

        AssertDictionary(entries, "requested permissions:", $"android.permission.INTERNET, android.permission.ACCESS_NETWORK_STATE");
    }

    [Test]
    public void CanParsePermissionsEmpty()
    {
        var packageName = "com.dummy.dummy";
        var contents = @$"
  Package [{packageName}] (ffc5f27):
    requested permissions:";

        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = EntriesToDictionary(parser.ParsePackageInformationAsPairs(packageName));

        AssertDictionary(entries, "requested permissions:", string.Empty);
    }

    [Test]
    public void CanParsePermissionsEmptyWithExtraLine()
    {
        var packageName = "com.dummy.dummy";
        var contents = @$"
  Package [{packageName}] (ffc5f27):
    requested permissions:
";
        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = EntriesToDictionary(parser.ParsePackageInformationAsPairs(packageName));

        AssertDictionary(entries, "requested permissions:", string.Empty);
    }

    [Test]
    public void CanParsePackageInfoAsSingleEntries()
    {
        var packageName = "com.DefaultCompany.AndroidEmptyGameActivity";
        var contents = @$"  Some other data

  Package [{packageName}] (ffc5f27):
    userId=10198
    sdsd";

        var parser = new AndroidLogcatPackageInfoParser(contents);
        var entries = parser.ParsePackageInformationAsSingleEntries(packageName);
        Assert.That(entries.Count, Is.EqualTo(2));
        StringAssert.AreEqualIgnoringCase("userId=10198", entries[0]);
        StringAssert.AreEqualIgnoringCase("sdsd", entries[1]);
    }
}
