using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Android.Logcat;

class AndroidLogcatDocumentationTests
{
    static readonly string[] kImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".svg" };

    // ![alt](path "title") and <img src="path">
    static readonly Regex kImageReference = new Regex(
        @"!\[[^\]]*\]\(\s*<?(?<path>[^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)|<img\s[^>]*src\s*=\s*[""'](?<path>[^""']+)[""']",
        RegexOptions.IgnoreCase);

    static string DocumentationRoot
    {
        get
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AndroidLogcatUtilities).Assembly);
            return Path.GetFullPath(Path.Combine(package.resolvedPath, "Documentation~"));
        }
    }

    static string RelativeToDocumentation(string path)
    {
        return Path.GetRelativePath(DocumentationRoot, path).Replace('\\', '/');
    }

    static bool IsImage(string path)
    {
        return kImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }

    static IEnumerable<(string markdown, string image)> GetImageReferences()
    {
        foreach (var markdown in Directory.GetFiles(DocumentationRoot, "*.md", SearchOption.AllDirectories))
        {
            var directory = Path.GetDirectoryName(markdown);
            foreach (Match match in kImageReference.Matches(File.ReadAllText(markdown)))
            {
                var path = Uri.UnescapeDataString(match.Groups["path"].Value.Split('#', '?')[0]);
                if (path.Contains("://"))
                    continue;
                yield return (RelativeToDocumentation(markdown), RelativeToDocumentation(Path.GetFullPath(Path.Combine(directory, path))));
            }
        }
    }

    // Compared ordinally: the docs are built and served on case-sensitive file systems,
    // so a reference that only differs in case works on Windows but is broken there.
    static HashSet<string> GetImageFiles(string folder)
    {
        return new HashSet<string>(
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(IsImage).Select(RelativeToDocumentation),
            StringComparer.Ordinal);
    }

    [Test]
    public void AllReferencedImagesExist()
    {
        var images = GetImageFiles(DocumentationRoot);
        var missing = GetImageReferences()
            .Where(r => !images.Contains(r.image))
            .Select(r => $"{r.markdown} -> {r.image}")
            .ToArray();

        Assert.IsEmpty(missing, "Documentation references images which don't exist:\n" + string.Join("\n", missing));
    }

    [Test]
    public void AllImagesAreReferenced()
    {
        var referenced = new HashSet<string>(GetImageReferences().Select(r => r.image), StringComparer.Ordinal);
        var unused = GetImageFiles(Path.Combine(DocumentationRoot, "images"))
            .Where(i => !referenced.Contains(i))
            .OrderBy(i => i)
            .ToArray();

        Assert.IsEmpty(unused, "Images not referenced by any documentation page:\n" + string.Join("\n", unused));
    }

    [Test]
    public void AllImageNamesAreLowerCase()
    {
        var invalid = GetImageFiles(DocumentationRoot)
            .Where(i => i != i.ToLowerInvariant())
            .OrderBy(i => i)
            .ToArray();

        Assert.IsEmpty(invalid, "Image names must be lower case:\n" + string.Join("\n", invalid));
    }
}
