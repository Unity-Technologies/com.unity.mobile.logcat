using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

class AndroidLogcatNetTests
{
    // Ensure we're running tests with .NET 3.5, because Unity 2018.3 and older don't have .NET 3.5 deprecated
    [Test]
    public void EnsureDotNET35IsUsed()
    {
#if !NET_2_0 && !UNITY_2019_2_OR_NEWER
        Assert.Fail("Tests project should be using .NET 3.5, did you modify Scripting Runtime Version?");
#endif
    }

    private UnityEditor.Compilation.Assembly GetLogcatAssembly()
    {
        var logcatAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Editor).FirstOrDefault(a => a.name.Equals("Unity.Mobile.AndroidLogcat.Editor"));
        Assert.IsNotNull(logcatAssembly, "Failed to find Android Logcat assembly");
        return logcatAssembly;
    }

    static bool IsUnity7OrNewer()
    {
        var versionString = Application.unityVersion;

        if (string.IsNullOrEmpty(versionString))
            return false;

        var majorEnd = versionString.IndexOf('.');
        if (majorEnd <= 0)
            return false;

        return int.TryParse(
            versionString.AsSpan(0, majorEnd),
            out var majorVersion) && majorVersion >= 7000;
    }

    /// <summary>
    /// Checks that no accidental references are added to Logcat assembly
    /// </summary>
    [Test]
    public void ValidateAssemblyReferences()
    {
        var logcatAssembly = GetLogcatAssembly();

        var expectedReferences = new List<string>(new[]
        {
            "mscorlib",
            "System",
            "System.Xml.Linq",
            "UnityEngine.IMGUIModule",
            "UnityEngine.CoreModule",
            "UnityEngine.VideoModule",
            "UnityEngine.TextRenderingModule",
            "UnityEngine.UIElementsModule",
            "System.Core",
            "UnityEngine.ImageConversionModule",
            "UnityEngine.JSONSerializeModule",
            "UnityEditor.CoreModule"
        });

        if (IsUnity7OrNewer())
        {   
            expectedReferences.AddRange(new[]
            {
            "Unity.Scripting",
            "UnityEngine.ScriptingModule",
            "UnityEngine.UICommonModule",
            "UnityEditor.Android.Extensions",
            });
        }

        var referencedCount = expectedReferences.ToDictionary(s => s, s => 0);

        // ReflectionOnlyLoadFrom is unsupported on CoreCLR; inspect the loaded assembly instead.
        var references = typeof(AndroidLogcatConsoleWindow).Assembly.GetReferencedAssemblies().Select(a => a.Name);
        var errors = new StringBuilder();
        Console.WriteLine($"Logcat package references:\n{string.Join("\n", references)}");
        foreach (var r in references)
        {
            if (!expectedReferences.Contains(r))
                errors.AppendLine($"Unexpected reference '{r}'");
            else
                referencedCount[r]++;
        }

        foreach (var r in referencedCount)
        {
            if (r.Value != 1)
                errors.AppendLine($"'{r.Key}' was expected to be referenced once, but was referenced {r.Value} times, please adjust expectations, maybe the reference is no longer needed?");
        }

        Assert.AreEqual(0, errors.Length, errors.ToString());
    }

    /// <summary>
    /// Check that we don't have unexpected using namespace
    /// For ex., using using NUnit.Framework is not valid
    /// This test is not perfect since you can access classes from those namespaces without using, but it's better than nothing
    /// </summary>
    [Test]
    public void ValidateUsingStatements()
    {
        var logcatAssembly = GetLogcatAssembly();
        var usingStatementRegex = new Regex(@"using\s+(?<RootNamespace>[a-zA-Z]+)\.*\S*;");

        var expectedNamespaces = new[]
        {
            "UnityEditor",
            "UnityEngine",
            "System"
        };

        var errors = new StringBuilder();
        foreach (var file in logcatAssembly.sourceFiles)
        {
            var contents = File.ReadAllText(file);
            var result = usingStatementRegex.Matches(contents);
            foreach (Match r in result)
            {
                var namezpace = r.Groups["RootNamespace"].Value;
                if (!expectedNamespaces.Contains(namezpace))
                {
                    errors.AppendLine($"Unexpected 'using {namezpace}...;' in {file}");
                }
            }
        }

        Assert.AreEqual(0, errors.Length, errors.ToString());
    }
}
