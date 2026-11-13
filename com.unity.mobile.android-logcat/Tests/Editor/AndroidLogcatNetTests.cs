using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;

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

    /// <summary>
    /// Check that we don't have unexpected using namespace
    /// For ex., using using NUnit.Framework is not valid
    /// This test is not perfect since you can access classes from those namespaces without using, but it's better than nothing
    /// </summary>
    [Test]
    public void ValidateUsingStatements()
    {
        var logcatAssembly = GetLogcatAssembly();
        var usingStatementRegex = new Regex(@"using\s+(?<Namespace>(?<RootNamespace>[a-zA-Z]+)\.*\S*);");

        // Any namespace under these roots is allowed
        var expectedRootNamespaces = new[]
        {
            "UnityEditor",
            "UnityEngine",
            "System",
        };

        // Only these exact namespaces are allowed
        var expectedNamespaces = new[]
        {
            "Unity.Profiling",
            "Unity.Scripting.LifecycleManagement",
        };

        var errors = new StringBuilder();
        foreach (var file in logcatAssembly.sourceFiles)
        {
            var contents = File.ReadAllText(file);
            var result = usingStatementRegex.Matches(contents);
            foreach (Match r in result)
            {
                var rootNamespace = r.Groups["RootNamespace"].Value;
                var namezpace = r.Groups["Namespace"].Value;
                if (!expectedRootNamespaces.Contains(rootNamespace) && !expectedNamespaces.Contains(namezpace))
                {
                    errors.AppendLine($"Unexpected 'using {namezpace};' in {file}");
                }
            }
        }

        Assert.AreEqual(0, errors.Length, errors.ToString());
    }
}
