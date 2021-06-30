using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;

public class AndroidLogcatNetTests
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
            "UnityEngine.IMGUIModule",
            "UnityEngine.CoreModule",
            "UnityEngine.TextRenderingModule",
            "System.Core",
            "UnityEngine.ImageConversionModule",
            "UnityEngine.JSONSerializeModule",
#if UNITY_2020_3_OR_NEWER
            "UnityEditor.CoreModule",
#else
            "UnityEditor",
#endif
        });

        var referencedCount = expectedReferences.ToDictionary(s => s, s => 0);

        var references = Assembly.ReflectionOnlyLoadFrom(logcatAssembly.outputPath).GetReferencedAssemblies().Select(a => a.Name);
        foreach (var r in references)
        {
            Assert.Contains(r, expectedReferences, $"Unexpected reference '{r}'");
            referencedCount[r]++;
        }

        foreach (var r in referencedCount)
        {
            Assert.AreEqual(1, r.Value, $"'{r.Key}' was expected to be referenced once, but was referenced {r.Value} times, please adjust expectations, maybe the reference is no longer needed?");
        }
    }

    /// <summary>
    /// Check that we don't have unexpected using <namespace>;
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
