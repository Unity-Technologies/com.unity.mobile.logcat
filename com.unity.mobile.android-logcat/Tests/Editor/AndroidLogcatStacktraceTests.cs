using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Unity.Android.Logcat;
using System.IO;
using System.Text.RegularExpressions;

public class AndroidLogcatStacktraceTests
{
    static string GetSymbolAddressUsingNM(AndroidTools tools, string symbolFilePath, string symbolName)
    {
        // With NDK 16, calling nm.exe shows an  error
        // System.Exception : L:\UnityInstalls\2019.1.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\aarch64-linux-android-4.9\prebuilt\windows-x86_64\bin\aarch64-linux-android-nm.exe -extern-only "L:\UnityInstalls\2019.1.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\Variations\il2cpp\Development\Symbols\arm64-v8a\libmain.sym.so"
        // returned with exit code 1
        // L:\UnityInstalls\2019.1.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\toolchains\aarch64-linux-android-4.9\prebuilt\windows-x86_64\bin\aarch64-linux-android-nm.exe: invalid option -- x
        var symbols = tools.RunNM(symbolFilePath);
        foreach (var s in symbols)
        {
            if (s.Contains(symbolName))
            {
                return "0x" + s.Split(' ')[0];
            }
        }
        return string.Empty;
    }

    static string GetSymbolAddressUsingReadElf(AndroidTools tools, string symbolFilePath, string symbolName)
    {
        // Regex for
        //     63: 000000000000083c   144 FUNC    GLOBAL DEFAULT   10 JNI_OnLoad

        var regex = new Regex(@".*:\s*(?<address>\S*).*");
        var symbols = tools.RunReadElf(symbolFilePath);
        foreach (var s in symbols)
        {
            if (s.Contains(symbolName))
            {
                var result = regex.Match(s);
                if (result.Success == false)
                    throw new System.Exception("Failed to regex " + s);
                return "0x" + result.Groups["address"];
            }
        }
        return string.Empty;
    }

    private static string GetSymbolPath(string abi, string libraryFile)
    {
        var playerPackage = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
        return AndroidLogcatUtilities.GetSymbolFile(Path.Combine(playerPackage, $"Variations/il2cpp/Development/Symbols/{abi}"), libraryFile);
    }

    private static string GetSymbolAddress(AndroidTools tools, string symbolPath, string symbolName)
    {
#if UNITY_2019_3_OR_NEWER
        var targetAddress = GetSymbolAddressUsingNM(tools, symbolPath, symbolName);
#else
        var targetAddress = GetSymbolAddressUsingReadElf(tools, symbolPath, symbolName);
#endif
        return targetAddress;
    }

    private void CanResolveStacktraces(string abi)
    {
        if (!AndroidBridge.AndroidExtensionsInstalled)
        {
            System.Console.WriteLine("Test ignored, because Android Support is not installed");
            return;
        }

        if (!AndroidLogcatTestsSetup.AndroidSDKAndNDKAvailable())
        {
            System.Console.WriteLine("Test ignored");
            return;
        }
        var tools = new AndroidTools();
        const string symbolName = "JNI_OnLoad";

        var expectedOutput = symbolName + " at ??:?";
        var symbolPath = GetSymbolPath(abi, "libmain.so");
        var targetAddress = GetSymbolAddress(tools, symbolPath, symbolName);

        Assert.IsNotEmpty(targetAddress, "Failed to find address for " + symbolName);
        var resolvedSymbols = tools.RunAddr2Line(symbolPath, new[] { targetAddress });
        Assert.IsTrue(resolvedSymbols.Length == 1, "Expected to resolve one symbol");
        Assert.AreEqual(expectedOutput, resolvedSymbols[0],
            string.Format("Failed to resolve symbol '{0}' for address '{1}'", symbolName, targetAddress));
    }

    [Test]
    public void CanResolveStacktracesARM64()
    {
        CanResolveStacktraces("arm64-v8a");
    }

    [Test]
    public void CanResolveStacktracesARMv7()
    {
        CanResolveStacktraces("armeabi-v7a");
    }

    [Test]
    public void CorrectyParseStacktraceCrash()
    {
        var logLines = new[]
        {
            "2020/07/15 15:31:30.887 23271 23292 Error AndroidRuntime    at libunity.0x0041e340(Native Method)",
            "2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 0041e340  /data/app/com.mygame==/lib/arm/libunity.so",
            "2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 0041e340  /data/app/com.mygame==/lib/x86/libunity.so",
            "2020/07/15 15:31:30.887 23271 23292 Error AndroidRuntime    at libunity.0x1234567890123456(Native Method)",
            "2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 1234567890123456  /data/app/com.mygame==/lib/arm64/libunity.so",
            "2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 1234567890123456  /data/app/com.mygame==/lib/x86_64/libunity.so",
        };

        var regexs = new List<ReordableListItem>();
        foreach (var r in AndroidLogcatSettings.kAddressResolveRegex)
        {
            regexs.Add(new ReordableListItem() { Enabled = true, Name = r });
        }


        for (int i = 0; i < logLines.Length; i++)
        {
            string expectedABI = string.Empty;
            switch (i)
            {
                case 1: expectedABI = AndroidLogcatUtilities.kAbiArmV7; break;
                case 2: expectedABI = AndroidLogcatUtilities.kAbiX86; break;
                case 4: expectedABI = AndroidLogcatUtilities.kAbiArm64; break;
                case 5: expectedABI = AndroidLogcatUtilities.kAbiX86_64; break;
            }

            var line = logLines[i];
            string address;
            string libName;
            string abi;
            var result = AndroidLogcatUtilities.ParseCrashLine(regexs, line, out abi, out address, out libName);
            Assert.IsTrue(result, "Failed to parse " + line);
            Assert.IsTrue(address.Equals("0041e340") || address.Equals("1234567890123456"));
            Assert.AreEqual("libunity.so", libName);
            Assert.AreEqual(expectedABI, abi);
        }
    }

    private static void AssertStringContains(string expected, string text)
    {
        Assert.IsTrue(text.Contains(expected), $"Expected string '{expected}' to be present in '{text}'");
    }

    [Test]
    public void CanCorrectlyPickSymbol()
    {
        if (!AndroidBridge.AndroidExtensionsInstalled)
        {
            System.Console.WriteLine("Test ignored, because Android Support is not installed");
            return;
        }

        if (!AndroidLogcatTestsSetup.AndroidSDKAndNDKAvailable())
        {
            System.Console.WriteLine("Test ignored, SDK & NDK are not available.");
            return;
        }

        var tools = new AndroidTools();
        var playerPackage = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
        var symbolsDirectory = Path.Combine(playerPackage, $"Variations/il2cpp/Development/Symbols");
        var symbolPaths = new List<ReordableListItem>(new[] { new ReordableListItem() { Enabled = true, Name = symbolsDirectory } });
        var symbolPathsArmV7 = new List<ReordableListItem>(new[] { new ReordableListItem() { Enabled = true, Name = Path.Combine(symbolsDirectory, AndroidLogcatUtilities.kAbiArmV7) } });
        var symbolPathsArm64 = new List<ReordableListItem>(new[] { new ReordableListItem() { Enabled = true, Name = Path.Combine(symbolsDirectory, AndroidLogcatUtilities.kAbiArm64) } });
        var libunity = "libunity";

        AssertStringContains(libunity, AndroidLogcatUtilities.GetSymbolFile(symbolPathsArmV7, string.Empty, libunity + ".so"));
        AssertStringContains(libunity, AndroidLogcatUtilities.GetSymbolFile(symbolPathsArm64, string.Empty, libunity + ".so"));
        // Since ABI is empty, we cannot resolve symbol path, thus the result will be empty
        Assert.AreEqual(string.Empty, AndroidLogcatUtilities.GetSymbolFile(symbolPaths, string.Empty, libunity + ".so"));

        var armv7Result = AndroidLogcatUtilities.GetSymbolFile(symbolPaths, AndroidLogcatUtilities.kAbiArmV7, libunity + ".so");
        AssertStringContains(libunity, armv7Result);
        AssertStringContains(AndroidLogcatUtilities.kAbiArmV7, armv7Result);

        var arm64Result = AndroidLogcatUtilities.GetSymbolFile(symbolPaths, AndroidLogcatUtilities.kAbiArm64, libunity + ".so");
        AssertStringContains(libunity, arm64Result);
        AssertStringContains(AndroidLogcatUtilities.kAbiArm64, arm64Result);
    }
}
