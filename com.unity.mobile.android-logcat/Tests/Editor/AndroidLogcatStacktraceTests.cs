using System.Collections.Generic;
using NUnit.Framework;
using Unity.Android.Logcat;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;

class AndroidLogcatStacktraceTests
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
        var symbols = tools.RunReadElf($"-Ws \"{symbolFilePath}\"");
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

    private static string GetSymbolsDirectory()
    {
        var playerPackage = AndroidLogcatUtilities.GetPlaybackEngineDirectory();
        return Path.Combine(playerPackage, $"Variations/il2cpp/Release/Symbols");
    }

    private static string GetSymbolPath(string abi, string libraryFile)
    {
        var path = Path.Combine(GetSymbolsDirectory(), abi);
        var result = AndroidLogcatUtilities.GetSymbolFile(path, libraryFile, AndroidLogcatSettings.kDefaultSymbolExtensions);

        if (string.IsNullOrEmpty(result))
            throw new System.Exception($"Failed to locate symbol file for {libraryFile} in '{path}'");
        return result;
    }

    internal static List<ReordableListItem> ToReordableList(string[] items)
    {
        return items.Select(i => new ReordableListItem() { Enabled = true, Name = i }).ToList();
    }

    internal static List<ReordableListItem> ToReordableList(string item)
    {
        return ToReordableList(new[] { item });
    }

    [TestCase("armeabi-v7a")]
    [TestCase("arm64-v8a")]
    public void CanResolveBuildIdFromSymbol(string abi)
    {
        if (!AndroidBridge.AndroidExtensionsInstalled)
            Assert.Ignore("Test ignored, because Android Support is not installed.");

        if (!AndroidLogcatTestsSetup.AndroidSDKAndNDKAvailable())
            Assert.Ignore("Test ignored, because SDK or NDK are not available.");

        var symbolPath = GetSymbolPath(abi, "libunity.so");
        var tools = new AndroidTools();
        var buildId = AndroidLogcatUtilities.GetBuildId(tools, symbolPath);

        // BuildId looks like this 4d911593b4008c7250197a60a320d872575ecc1b
        Assert.AreEqual(40, buildId.Length, $"Unexpected build id string length: {buildId} (Length = {buildId.Length})");
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
            "  #15  pc 0x0000000000a0de84  /data/app/com.DefaultCompany.NativeRuntimeException1-eStyrW-dxxC0QfRH6veLhA==/lib/arm64/libunity.so",
            "2025/05/14 15:30:41.520 2672 2694 Error CRASH       #01 pc 1234567890123456  /data/app/com.DefaultCompany.ForceCrash-YCyru7yBnQrlx6Q4p4Am_w==/lib/arm64/libunity.so (Utils_CUSTOM_ForceCrash(DiagnosticsUtils_Bindings::ForcedCrashCategory)+60) (BuildId: 4d911593b4008c7250197a60a320d872575ecc1b)"
        };

        var expectedABIs = new[]
        {
            string.Empty,
            AndroidLogcatUtilities.kAbiArmV7,
            AndroidLogcatUtilities.kAbiX86,
            string.Empty,
            AndroidLogcatUtilities.kAbiArm64,
            AndroidLogcatUtilities.kAbiX86_64,
            AndroidLogcatUtilities.kAbiArm64,
            AndroidLogcatUtilities.kAbiArm64
        };

        var expectedBuildIds = new[]
{
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "4d911593b4008c7250197a60a320d872575ecc1b"
        };

        var regexs = new List<ReordableListItem>();
        foreach (var r in AndroidLogcatSettings.kAddressResolveRegex)
        {
            regexs.Add(new ReordableListItem() { Enabled = true, Name = r });
        }


        for (int i = 0; i < logLines.Length; i++)
        {
            UnityEngine.Debug.Log($"Validating: {logLines[i]}");
            var line = logLines[i];
            var result = AndroidLogcatUtilities.ParseCrashLine(regexs, line, out var abi, out var address, out var libName, out var buildId);
            Assert.IsTrue(result, "Failed to parse " + line);
            Assert.IsTrue(address.Equals("0041e340") || address.Equals("1234567890123456") || address.Equals("0x0000000000a0de84"), $"Invalid resolved address: {address}");
            StringAssert.AreEqualIgnoringCase("libunity.so", libName);
            StringAssert.AreEqualIgnoringCase(expectedABIs[i], abi);
            StringAssert.AreEqualIgnoringCase(expectedBuildIds[i], buildId);
        }
    }

    private static void AssertStringContains(string expected, string text)
    {
        Assert.IsTrue(text.Contains(expected), $"Expected string '{expected}' to be present in '{text}'");
    }


    class CanResolveStacktraces
    {
        AndroidTools m_Tools;
        List<ReordableListItem> m_SymbolRegexes;
        List<ReordableListItem> m_SymbolDirectories;
        List<ReordableListItem> m_SymbolExtensions;
        Dictionary<AndroidArchitecture, string> m_BuildId;
        Dictionary<AndroidArchitecture, string> m_AddressJNI_OnLoad;
        Dictionary<AndroidArchitecture, string> m_AddressUnitySendMessage;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                Assert.Ignore("Test ignored, because Android Support is not installed.");

            if (!AndroidLogcatTestsSetup.AndroidSDKAndNDKAvailable())
                Assert.Ignore("Test ignored, because SDK or NDK are not available.");

            m_Tools = new AndroidTools();
            m_SymbolRegexes = ToReordableList(AndroidLogcatSettings.kAddressResolveRegex);
            m_SymbolDirectories = ToReordableList(GetSymbolsDirectory());
            m_SymbolExtensions = ToReordableList(AndroidLogcatSettings.kDefaultSymbolExtensions);

            var architectures = new[] { AndroidArchitecture.ARMv7, AndroidArchitecture.ARM64 };
            m_BuildId = new Dictionary<AndroidArchitecture, string>();
            m_AddressJNI_OnLoad = new Dictionary<AndroidArchitecture, string>();
            m_AddressUnitySendMessage = new Dictionary<AndroidArchitecture, string>();

            foreach (var architecture in architectures)
            {
                var symbolPath = GetSymbolPath(architecture.ToABI(), "libunity.so");
                m_BuildId[architecture] = AndroidLogcatUtilities.GetBuildId(m_Tools, symbolPath);

                var targetAddressJNI_OnLoad = GetSymbolAddressUsingNM(m_Tools, symbolPath, "JNI_OnLoad");
                Assert.IsNotEmpty(targetAddressJNI_OnLoad, "Failed to find address for JNI_OnLoad");
                m_AddressJNI_OnLoad[architecture] = targetAddressJNI_OnLoad; ;

                var targetAddressUnitySendMessage = GetSymbolAddressUsingNM(m_Tools, symbolPath, "UnitySendMessage");
                Assert.IsNotEmpty(targetAddressUnitySendMessage, "Failed to find address for UnitySendMessage");
                m_AddressUnitySendMessage[architecture] = targetAddressUnitySendMessage;

            }

        }

        [TestCase(AndroidArchitecture.ARMv7)]
        [TestCase(AndroidArchitecture.ARM64)]
        public void WithCorrectBuildId(AndroidArchitecture architecture)
        {
            var ndkArchitecture = architecture.ToNdkArchitecture();

            var dummyStacktrace = new[]
            {
                $"2025/05/26 13:13:11.488 16541 16557 Error CRASH       #00 pc {m_AddressJNI_OnLoad[architecture]}  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.ForceCrash-gYtNtB9HCft5sX98ZKtsTQ==/lib/{ndkArchitecture}/libunity.so (BuildId: {m_BuildId[architecture]})",
                $"2025/05/26 13:13:11.488 16541 16557 Error CRASH       #01 pc {m_AddressUnitySendMessage[architecture]}  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.ForceCrash-gYtNtB9HCft5sX98ZKtsTQ==/lib/{ndkArchitecture}/libunity.so (BuildId: {m_BuildId[architecture]})",
             };

            var result = AndroidLogcatStacktraceWindow.ResolveAddresses(dummyStacktrace,
                m_SymbolRegexes,
                m_SymbolDirectories,
                m_SymbolExtensions,
                m_Tools);

            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorsAndWarnings), result.ErrorsAndWarnings);
            StringAssert.Contains("JNI_OnLoad", result.Result);
            StringAssert.Contains("UnitySendMessage", result.Result);
        }

        [TestCase(AndroidArchitecture.ARMv7)]
        [TestCase(AndroidArchitecture.ARM64)]
        public void WithWrongBuildId(AndroidArchitecture architecture)
        {
            var ndkArchitecture = architecture.ToNdkArchitecture();
            var wrongBuildId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var dummyStacktrace = new[]
            {
                $"2025/05/26 13:13:11.488 16541 16557 Error CRASH       #00 pc {m_AddressJNI_OnLoad[architecture]}  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.ForceCrash-gYtNtB9HCft5sX98ZKtsTQ==/lib/{ndkArchitecture}/libunity.so (BuildId: {wrongBuildId})",
                $"2025/05/26 13:13:11.488 16541 16557 Error CRASH       #01 pc {m_AddressUnitySendMessage[architecture]}  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.ForceCrash-gYtNtB9HCft5sX98ZKtsTQ==/lib/{ndkArchitecture}/libunity.so (BuildId: {wrongBuildId})",
             };

            var result = AndroidLogcatStacktraceWindow.ResolveAddresses(dummyStacktrace,
                m_SymbolRegexes,
                m_SymbolDirectories,
                m_SymbolExtensions,
                m_Tools);

            StringAssert.Contains("Wrong symbol files?", result.ErrorsAndWarnings);
            StringAssert.Contains("JNI_OnLoad", result.Result);
            StringAssert.Contains("UnitySendMessage", result.Result);
        }

        [TestCase(AndroidArchitecture.ARMv7)]
        [TestCase(AndroidArchitecture.ARM64)]
        public void WithMissingBuildId(AndroidArchitecture architecture)
        {
            var ndkArchitecture = architecture.ToNdkArchitecture();
            var dummyStacktrace = new[]
            {
                $"2025/05/26 13:13:11.488 16541 16557 Error CRASH       #00 pc {m_AddressJNI_OnLoad[architecture]}  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.ForceCrash-gYtNtB9HCft5sX98ZKtsTQ==/lib/{ndkArchitecture}/libunity.so",
                $"2025/05/26 13:13:11.488 16541 16557 Error CRASH       #01 pc {m_AddressUnitySendMessage[architecture]}  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.ForceCrash-gYtNtB9HCft5sX98ZKtsTQ==/lib/{ndkArchitecture}/libunity.so",
             };

            var result = AndroidLogcatStacktraceWindow.ResolveAddresses(dummyStacktrace,
                m_SymbolRegexes,
                m_SymbolDirectories,
                m_SymbolExtensions,
                m_Tools);

            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorsAndWarnings), result.ErrorsAndWarnings);
            StringAssert.Contains("JNI_OnLoad", result.Result);
            StringAssert.Contains("UnitySendMessage", result.Result);
        }


        [Test]
        public void CanCorrectlyPickSymbol()
        {
            var symbolsDirectory = GetSymbolsDirectory();
            var symbolPaths = ToReordableList(symbolsDirectory);
            var symbolPathsArmV7 = ToReordableList(Path.Combine(symbolsDirectory, AndroidLogcatUtilities.kAbiArmV7));
            var symbolPathsArm64 = ToReordableList(Path.Combine(symbolsDirectory, AndroidLogcatUtilities.kAbiArm64));
            var libunity = "libunity";

            AssertStringContains(libunity, AndroidLogcatUtilities.GetSymbolFile(symbolPathsArmV7, string.Empty, libunity + ".so", AndroidLogcatSettings.kDefaultSymbolExtensions));
            AssertStringContains(libunity, AndroidLogcatUtilities.GetSymbolFile(symbolPathsArm64, string.Empty, libunity + ".so", AndroidLogcatSettings.kDefaultSymbolExtensions));
            // Since ABI is empty, we cannot resolve symbol path, thus the result will be empty
            Assert.AreEqual(string.Empty, AndroidLogcatUtilities.GetSymbolFile(symbolPaths, string.Empty, libunity + ".so", AndroidLogcatSettings.kDefaultSymbolExtensions));

            var armv7Result = AndroidLogcatUtilities.GetSymbolFile(symbolPaths, AndroidLogcatUtilities.kAbiArmV7, libunity + ".so", AndroidLogcatSettings.kDefaultSymbolExtensions);
            AssertStringContains(libunity, armv7Result);
            AssertStringContains(AndroidLogcatUtilities.kAbiArmV7, armv7Result);

            var arm64Result = AndroidLogcatUtilities.GetSymbolFile(symbolPaths, AndroidLogcatUtilities.kAbiArm64, libunity + ".so", AndroidLogcatSettings.kDefaultSymbolExtensions);
            AssertStringContains(libunity, arm64Result);
            AssertStringContains(AndroidLogcatUtilities.kAbiArm64, arm64Result);
        }
    }
}
