using System.Collections.Generic;
using NUnit.Framework;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;
using System.Linq;
using System.Text;

class AndroidLogcatPacakgeInfoTests
{
    [Test]
    public void CanParsePackageInfo()
    {
        var contents = @"  Package [com.DefaultCompany.AndroidEmptyGameActivity] (ffc5f27):
    userId=10198
    pkg=Package{d78b8ca com.DefaultCompany.AndroidEmptyGameActivity}
    codePath=/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/com.DefaultCompany.AndroidEmptyGameActivity-bzIOds7vr6jRoCaEw1TacQ==
    resourcePath=/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/com.DefaultCompany.AndroidEmptyGameActivity-bzIOds7vr6jRoCaEw1TacQ==
    legacyNativeLibraryDir=/data/app/~~ur3u-ye7dI8GpfIRkeYy7Q==/com.DefaultCompany.AndroidEmptyGameActivity-bzIOds7vr6jRoCaEw1TacQ==/lib
    primaryCpuAbi=armeabi-v7a
    secondaryCpuAbi=null
    versionCode=1 minSdk=22 targetSdk=32
    versionName=0.1
    splits=[base]
    apkSigningVersion=2
    applicationInfo=ApplicationInfo{d78b8ca com.DefaultCompany.AndroidEmptyGameActivity}
    flags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA TEST_ONLY ALLOW_BACKUP ]
    privateFlags=[ PRIVATE_FLAG_ACTIVITIES_RESIZE_MODE_RESIZEABLE_VIA_SDK_VERSION ALLOW_AUDIO_PLAYBACK_CAPTURE PRIVATE_FLAG_ALLOW_NATIVE_HEAP_POINTER_TAGGING ]
    forceQueryable=false
    queriesPackages=[]
    dataDir=/data/user/0/com.DefaultCompany.AndroidEmptyGameActivity
    supportsScreens=[small, medium, large, xlarge, resizeable, anyDensity]
    timeStamp=2022-09-19 15:52:25
    firstInstallTime=2022-09-13 16:14:57
    lastUpdateTime=2022-09-19 15:52:26
    signatures=PackageSignatures{af06a3b version:2, signatures:[ca97cd1f], past signatures:[]}
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

        var errors = new StringBuilder();
        var lines = contents.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimStart()).ToArray();

        if (lines.Length == 0)
            throw new System.Exception("No package info found");
        var packageName = Regex.Escape("com.DefaultCompany.AndroidEmptyGameActivity");
        var title = new Regex($"Package.*{packageName}.*\\:");

        if (!title.Match(lines[0]).Success)
            throw new System.Exception($"Expected 'Package [<package_name>] (<id>) :', but got '{lines[0]}'");

        var keyValueRegex = new Regex(@"(?<key>\S+)\=(?<value>.*)");
        for (int i = 1; i < lines.Length; i++)
        {
            var l = lines[i];
            // Check if next lines are list
            while (l.EndsWith("permissions:"))
            {
                UnityEngine.Debug.Log($"{l}");
                i++;
                // List permissions
                while (i < lines.Length)
                {
                    var entry = lines[i];
                    if (entry.StartsWith("android.permission"))
                    {
                        UnityEngine.Debug.Log($"P-->   {entry}");
                        i++;
                    }
                    else
                    {
                        l = lines[i];
                        break;
                    }
                }

                if (i >= lines.Length)
                    break;
            }

            if (i >= lines.Length)
                break;

            var result = keyValueRegex.Match(l);

            if (result.Success)
            {
                var key = result.Groups["key"];
                var value = result.Groups["value"];

                UnityEngine.Debug.Log($"{key}={value}");
                continue;
            }


            errors.AppendLine($"Failed to resolve {l}");
        }

        if (errors.Length > 0)
        {
            UnityEngine.Debug.LogError($"Found errors:\n{errors.ToString()}\n\nin\n{contents}");
        }

    }
}
