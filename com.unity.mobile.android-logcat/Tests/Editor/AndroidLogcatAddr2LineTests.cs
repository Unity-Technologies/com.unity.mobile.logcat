using NUnit.Framework;
using UnityEditor;
using UnityEditor.Android;
using Unity.Android.Logcat;
using System.IO;

public class AndroidLogcatAddr2LineTests
{
    [Test]
    public void CanResolveStacktraces()
    {
        var tools = new AndroidTools();

        const string symbolName = "JNI_OnLoad";
        var playerPackage = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
        var expectedOutput = symbolName + " at ??:?";
        var symbolPath = Path.GetFullPath(Path.Combine(playerPackage, "Variations/il2cpp/Development/Symbols/arm64-v8a/libmain.sym.so"));
        var symbols = tools.RunNM(symbolPath);
        string targetAddress = "";

        foreach (var s in symbols)
        {
            if (s.Contains(symbolName))
            {
                targetAddress = "0x" + s.Split(' ')[0];
                break;
            }
        }

        Assert.IsNotEmpty(targetAddress, "Failed to find address for " + symbolName);
        var resolvedSymbols = tools.RunAddr2Line(symbolPath, new[] { targetAddress });
        Assert.IsTrue(resolvedSymbols.Length == 1, "Expected to resolve one symbol");
        Assert.AreEqual(expectedOutput, resolvedSymbols[0]);
    }
}
