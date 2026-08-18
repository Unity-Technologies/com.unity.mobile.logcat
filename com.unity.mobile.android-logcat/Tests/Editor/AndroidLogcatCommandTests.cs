using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Android.Logcat;

class AndroidLogcatCommandPlaceholderTests
{
    [Test]
    public void HasPlaceholders_DetectsTokens()
    {
        Assert.IsTrue(AndroidLogcatCommandPlaceholders.HasPlaceholders("adb shell pm clear <package>"));
        Assert.IsFalse(AndroidLogcatCommandPlaceholders.HasPlaceholders("adb devices"));
        Assert.IsFalse(AndroidLogcatCommandPlaceholders.HasPlaceholders(""));
        Assert.IsFalse(AndroidLogcatCommandPlaceholders.HasPlaceholders(null));
    }

    [Test]
    public void Parse_ReturnsTokensInOrder()
    {
        var result = AndroidLogcatCommandPlaceholders.Parse("adb push <local> <remote>");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("local", result[0].Token);
        Assert.AreEqual("remote", result[1].Token);
    }

    [Test]
    public void Parse_DeduplicatesTokensCaseInsensitively()
    {
        var result = AndroidLogcatCommandPlaceholders.Parse("adb shell am start -n <package>/<Package>.MainActivity <PACKAGE>");

        Assert.AreEqual(1, result.Count, "Tokens differing only by case should collapse into a single entry");
        Assert.AreEqual("package", result[0].Token);
    }

    [Test]
    public void Parse_AppliesDefaultValueProvider()
    {
        var result = AndroidLogcatCommandPlaceholders.Parse(
            "adb shell pm clear <package> <other>",
            token => token == "package" ? "com.unity.test" : string.Empty);

        Assert.AreEqual("com.unity.test", result.First(p => p.Token == "package").Value);
        Assert.AreEqual(string.Empty, result.First(p => p.Token == "other").Value);
    }

    [Test]
    public void Parse_NullDefaultProviderYieldsEmptyValue()
    {
        var result = AndroidLogcatCommandPlaceholders.Parse("adb shell pm clear <package>", _ => null);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(string.Empty, result[0].Value);
    }

    [Test]
    public void Parse_EmptyOrNullCommandReturnsEmptyList()
    {
        Assert.AreEqual(0, AndroidLogcatCommandPlaceholders.Parse(null).Count);
        Assert.AreEqual(0, AndroidLogcatCommandPlaceholders.Parse("").Count);
        Assert.AreEqual(0, AndroidLogcatCommandPlaceholders.Parse("adb devices").Count);
    }

    [Test]
    public void Resolve_ReplacesAllOccurrencesOfRepeatedToken()
    {
        var command = "adb shell am start -n <package>/<package>.MainActivity";
        var placeholders = AndroidLogcatCommandPlaceholders.Parse(command);
        placeholders[0].Value = "com.unity.test";

        var resolved = AndroidLogcatCommandPlaceholders.Resolve(command, placeholders);

        Assert.AreEqual("adb shell am start -n com.unity.test/com.unity.test.MainActivity", resolved);
    }

    [Test]
    public void Resolve_ReplacesTokensDifferingByCase()
    {
        var command = "adb shell <Package> <package>";
        var placeholders = AndroidLogcatCommandPlaceholders.Parse(command);
        placeholders[0].Value = "abc";

        Assert.AreEqual("adb shell abc abc", AndroidLogcatCommandPlaceholders.Resolve(command, placeholders));
    }

    [Test]
    public void Resolve_LeavesUnknownTokensIntact()
    {
        var command = "adb push <local> <remote>";
        var placeholders = new List<AndroidLogcatCommandPlaceholders.Placeholder>
        {
            new AndroidLogcatCommandPlaceholders.Placeholder("local", "/tmp/file")
        };

        Assert.AreEqual("adb push /tmp/file <remote>", AndroidLogcatCommandPlaceholders.Resolve(command, placeholders));
    }

    [Test]
    public void Resolve_HandlesNullInputsGracefully()
    {
        Assert.IsNull(AndroidLogcatCommandPlaceholders.Resolve(null, null));
        Assert.AreEqual("adb devices", AndroidLogcatCommandPlaceholders.Resolve("adb devices", null));
    }

    [Test]
    public void Resolve_EmptyValueRemovesToken()
    {
        var command = "adb shell input text <text>";
        var placeholders = AndroidLogcatCommandPlaceholders.Parse(command);
        placeholders[0].Value = "";

        Assert.AreEqual("adb shell input text ", AndroidLogcatCommandPlaceholders.Resolve(command, placeholders));
    }
}

class AndroidLogcatCommandParserTests
{
    [Test]
    public void SplitLines_HandlesAllLineEndings()
    {
        Assert.AreEqual(new[] { "a", "b", "c" }, AndroidLogcatCommandParser.SplitLines("a\r\nb\rc").ToArray());
        Assert.AreEqual(new[] { "a", "b" }, AndroidLogcatCommandParser.SplitLines("a\nb").ToArray());
    }

    [Test]
    public void SplitLines_TrimsAndDropsEmptyLines()
    {
        var result = AndroidLogcatCommandParser.SplitLines("  adb devices  \n\n\t\n adb reboot ");

        Assert.AreEqual(new[] { "adb devices", "adb reboot" }, result.ToArray());
    }

    [Test]
    public void SplitLines_NullOrEmptyReturnsEmpty()
    {
        Assert.AreEqual(0, AndroidLogcatCommandParser.SplitLines(null).Count);
        Assert.AreEqual(0, AndroidLogcatCommandParser.SplitLines("   ").Count);
    }

    /// <summary>
    /// adb on Windows emits "\r\r\n" for shell output. Splitting on '\r' would produce two empty
    /// lines per real line, which rendered as visibly double spaced output in the Editor.
    /// </summary>
    [Test]
    public void SplitOutputLines_HandlesAdbDoubleCarriageReturn()
    {
        var adbOutput = "package:com.google.android.euicc\r\r\npackage:com.android.dynsystem\r\r\n";

        var result = AndroidLogcatCommandParser.SplitOutputLines(adbOutput);

        // Trailing newline yields one final empty entry; the real lines must not be padded.
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("package:com.google.android.euicc", result[0]);
        Assert.AreEqual("package:com.android.dynsystem", result[1]);
        Assert.AreEqual("", result[2]);
    }

    [Test]
    public void SplitOutputLines_StripsCarriageReturnsFromLineEnds()
    {
        var result = AndroidLogcatCommandParser.SplitOutputLines("a\r\nb\r\nc");

        Assert.AreEqual(new[] { "a", "b", "c" }, result);
        foreach (var line in result)
            Assert.IsFalse(line.Contains("\r"), "No line may retain a carriage return");
    }

    [Test]
    public void SplitOutputLines_PreservesIntentionalBlankLines()
    {
        // dumpsys and similar tools use blank lines as section separators.
        var result = AndroidLogcatCommandParser.SplitOutputLines("section one\n\nsection two");

        Assert.AreEqual(new[] { "section one", "", "section two" }, result);
    }

    [Test]
    public void SplitOutputLines_LoneCarriageReturnsDoNotCreateBlankLines()
    {
        // Old Mac style endings are rare, but must not silently join or double up.
        Assert.AreEqual(new[] { "ab" }, AndroidLogcatCommandParser.SplitOutputLines("a\rb"));
    }

    [Test]
    public void SplitOutputLines_EmptyOrNullReturnsEmptyArray()
    {
        Assert.AreEqual(0, AndroidLogcatCommandParser.SplitOutputLines(null).Length);
        Assert.AreEqual(0, AndroidLogcatCommandParser.SplitOutputLines("").Length);
    }

    [Test]
    public void IsAdbCommand_RecognizesPrefixCaseInsensitively()
    {
        Assert.IsTrue(AndroidLogcatCommandParser.IsAdbCommand("adb devices"));
        Assert.IsTrue(AndroidLogcatCommandParser.IsAdbCommand("ADB devices"));
        Assert.IsTrue(AndroidLogcatCommandParser.IsAdbCommand("  adb devices"));
        Assert.IsFalse(AndroidLogcatCommandParser.IsAdbCommand("java -jar bundletool.jar"));
        Assert.IsFalse(AndroidLogcatCommandParser.IsAdbCommand("adb"), "Bare 'adb' has no arguments and is not a runnable adb command");
        Assert.IsFalse(AndroidLogcatCommandParser.IsAdbCommand(null));
    }

    [Test]
    public void StripAdbPrefix_RemovesPrefixOnly()
    {
        Assert.AreEqual("devices -l", AndroidLogcatCommandParser.StripAdbPrefix("adb devices -l"));
        Assert.AreEqual("devices", AndroidLogcatCommandParser.StripAdbPrefix("  adb   devices  "));
        Assert.AreEqual("java -jar x.jar", AndroidLogcatCommandParser.StripAdbPrefix("java -jar x.jar"));
    }

    [Test]
    public void SpecifiesDevice_DetectsExplicitSerialFlag()
    {
        Assert.IsTrue(AndroidLogcatCommandParser.SpecifiesDevice("-s emulator-5554 shell ls"));
        Assert.IsTrue(AndroidLogcatCommandParser.SpecifiesDevice("--serial emulator-5554 shell ls"));
        Assert.IsFalse(AndroidLogcatCommandParser.SpecifiesDevice("shell ls"));
        Assert.IsFalse(AndroidLogcatCommandParser.SpecifiesDevice(""));
        Assert.IsFalse(AndroidLogcatCommandParser.SpecifiesDevice(null));
    }

    [Test]
    public void SpecifiesDevice_IgnoresSerialFlagAfterSubcommand()
    {
        // Here '-s' is an argument to logcat, not adb's device selector.
        Assert.IsFalse(AndroidLogcatCommandParser.SpecifiesDevice("shell dumpsys -s foo"));
    }

    [Test]
    public void Tokenize_RespectsDoubleQuotes()
    {
        var result = AndroidLogcatCommandParser.Tokenize("\"C:\\Program Files\\java.exe\" -jar tool.jar");

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("C:\\Program Files\\java.exe", result[0]);
        Assert.AreEqual("-jar", result[1]);
        Assert.AreEqual("tool.jar", result[2]);
    }

    [Test]
    public void Tokenize_RespectsSingleQuotes()
    {
        var result = AndroidLogcatCommandParser.Tokenize("echo 'hello world' done");

        Assert.AreEqual(new[] { "echo", "hello world", "done" }, result.ToArray());
    }

    [Test]
    public void Tokenize_CollapsesRepeatedWhitespace()
    {
        Assert.AreEqual(new[] { "a", "b" }, AndroidLogcatCommandParser.Tokenize("  a \t  b  ").ToArray());
    }

    [Test]
    public void Tokenize_EmptyReturnsEmpty()
    {
        Assert.AreEqual(0, AndroidLogcatCommandParser.Tokenize("").Count);
        Assert.AreEqual(0, AndroidLogcatCommandParser.Tokenize(null).Count);
    }

    [Test]
    public void TrySplitExecutable_HandlesQuotedPathWithSpaces()
    {
        Assert.IsTrue(AndroidLogcatCommandParser.TrySplitExecutable(
            "\"C:\\Program Files\\Java\\java.exe\" -jar bundletool.jar", out var exe, out var args));

        Assert.AreEqual("C:\\Program Files\\Java\\java.exe", exe);
        Assert.AreEqual("-jar bundletool.jar", args);
    }

    [Test]
    public void TrySplitExecutable_RequotesArgumentsContainingSpaces()
    {
        Assert.IsTrue(AndroidLogcatCommandParser.TrySplitExecutable(
            "java -jar \"my tool.jar\"", out var exe, out var args));

        Assert.AreEqual("java", exe);
        Assert.AreEqual("-jar \"my tool.jar\"", args);
    }

    [Test]
    public void TrySplitExecutable_NoArgumentsYieldsEmptyArgs()
    {
        Assert.IsTrue(AndroidLogcatCommandParser.TrySplitExecutable("adb", out var exe, out var args));

        Assert.AreEqual("adb", exe);
        Assert.AreEqual(string.Empty, args);
    }

    [Test]
    public void TrySplitExecutable_EmptyCommandFails()
    {
        Assert.IsFalse(AndroidLogcatCommandParser.TrySplitExecutable("   ", out _, out _));
        Assert.IsFalse(AndroidLogcatCommandParser.TrySplitExecutable(null, out _, out _));
    }
}

class AndroidLogcatCommandMatcherTests
{
    [Test]
    public void Matches_IsCaseInsensitiveOnNameAndCommand()
    {
        var entry = new AndroidLogcatCommandEntry("List Devices", "adb devices");

        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, "list"));
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, "DEVICES"));
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, "adb dev"));
        Assert.IsFalse(AndroidLogcatCommandMatcher.Matches(entry, "logcat"));
    }

    [Test]
    public void Matches_NullEntryDoesNotThrow()
    {
        Assert.IsFalse(AndroidLogcatCommandMatcher.Matches(null, "anything"));
    }

    [Test]
    public void Matches_EntryWithNullFieldsDoesNotThrow()
    {
        var entry = new AndroidLogcatCommandEntry(null, null);

        Assert.IsFalse(AndroidLogcatCommandMatcher.Matches(entry, "abc"));
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, ""), "An empty search term matches everything");
    }

    [Test]
    public void Matches_PartiallyNullEntryStillMatchesPopulatedField()
    {
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(new AndroidLogcatCommandEntry("Name", null), "name"));
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(new AndroidLogcatCommandEntry(null, "adb devices"), "adb"));
    }

    [Test]
    public void Matches_EmptySearchTermMatchesEverything()
    {
        var entry = new AndroidLogcatCommandEntry("List Devices", "adb devices");

        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, null));
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, "   "));
    }

    [Test]
    public void Matches_CategoryFilterIsApplied()
    {
        var entry = new AndroidLogcatCommandEntry("List Devices", "adb devices", AndroidLogcatCommandCategory.DeviceManagement);

        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, AndroidLogcatCommandCategory.DeviceManagement, ""));
        Assert.IsFalse(AndroidLogcatCommandMatcher.Matches(entry, AndroidLogcatCommandCategory.Quest, ""));
        Assert.IsTrue(AndroidLogcatCommandMatcher.Matches(entry, null, ""), "A null category means no category filtering");
    }

    [Test]
    public void GetCategoryDisplayName_ReturnsNonEmptyForEveryCategory()
    {
        foreach (AndroidLogcatCommandCategory category in System.Enum.GetValues(typeof(AndroidLogcatCommandCategory)))
            Assert.IsFalse(string.IsNullOrEmpty(AndroidLogcatCommandMatcher.GetCategoryDisplayName(category)),
                $"Category {category} has no display name");
    }
}

class AndroidLogcatCommandImportTests
{
    [Test]
    public void Sanitize_DropsNullEntries()
    {
        var result = AndroidLogcatCommandImport.Sanitize(new[]
        {
            null,
            new AndroidLogcatCommandEntry("Valid", "adb devices"),
            null
        });

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Valid", result[0].name);
    }

    [Test]
    public void Sanitize_DropsEntriesMissingNameOrCommand()
    {
        var result = AndroidLogcatCommandImport.Sanitize(new[]
        {
            new AndroidLogcatCommandEntry(null, "adb devices"),
            new AndroidLogcatCommandEntry("No Command", null),
            new AndroidLogcatCommandEntry("", "adb devices"),
            new AndroidLogcatCommandEntry("Whitespace", "   "),
            new AndroidLogcatCommandEntry("Valid", "adb devices")
        });

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Valid", result[0].name);
    }

    [Test]
    public void Sanitize_TrimsSurvivingEntries()
    {
        var result = AndroidLogcatCommandImport.Sanitize(new[]
        {
            new AndroidLogcatCommandEntry("  Padded  ", "  adb devices  ")
        });

        Assert.AreEqual("Padded", result[0].name);
        Assert.AreEqual("adb devices", result[0].command);
    }

    [Test]
    public void Sanitize_PreservesCategory()
    {
        var result = AndroidLogcatCommandImport.Sanitize(new[]
        {
            new AndroidLogcatCommandEntry("Quest Thing", "adb shell x", AndroidLogcatCommandCategory.Quest)
        });

        Assert.AreEqual(AndroidLogcatCommandCategory.Quest, result[0].category);
    }

    [Test]
    public void Sanitize_NullInputReturnsEmptyList()
    {
        Assert.AreEqual(0, AndroidLogcatCommandImport.Sanitize(null).Count);
    }

    [Test]
    public void IsValid_RequiresBothFields()
    {
        Assert.IsTrue(new AndroidLogcatCommandEntry("a", "b").IsValid);
        Assert.IsFalse(new AndroidLogcatCommandEntry("a", "").IsValid);
        Assert.IsFalse(new AndroidLogcatCommandEntry("", "b").IsValid);
        Assert.IsFalse(new AndroidLogcatCommandEntry(null, null).IsValid);
    }
}

class AndroidLogcatCommandCatalogTests
{
    [Test]
    public void Catalog_AllEntriesAreValid()
    {
        foreach (var entry in AndroidLogcatAdbCommandCatalog.All)
            Assert.IsTrue(entry.IsValid, $"Catalog entry '{entry.name}' is missing a name or command");
    }

    [Test]
    public void Catalog_HasNoDuplicateCommands()
    {
        var duplicates = AndroidLogcatAdbCommandCatalog.All
            .GroupBy(e => e.command, System.StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.IsEmpty(duplicates, "Duplicate commands in catalog: " + string.Join(", ", duplicates));
    }

    [Test]
    public void Catalog_HasNoDuplicateNames()
    {
        var duplicates = AndroidLogcatAdbCommandCatalog.All
            .GroupBy(e => e.name, System.StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.IsEmpty(duplicates, "Duplicate names in catalog: " + string.Join(", ", duplicates));
    }

    [Test]
    public void Catalog_EveryEntryHasACategory()
    {
        foreach (var entry in AndroidLogcatAdbCommandCatalog.All)
            Assert.AreNotEqual(AndroidLogcatCommandCategory.Uncategorized, entry.category,
                $"Catalog entry '{entry.name}' is uncategorized, so it would not appear under any filter chip");
    }

    /// <summary>
    /// The catalog must not duplicate functionality the package already provides as a dedicated
    /// feature. See the scope note on AndroidLogcatAdbCommandCatalog.
    /// </summary>
    [Test]
    public void Catalog_DoesNotDuplicateExistingPackageFeatures()
    {
        foreach (var entry in AndroidLogcatAdbCommandCatalog.All)
        {
            var command = entry.command.ToLowerInvariant();

            Assert.IsFalse(command.Contains("logcat"),
                $"'{entry.name}' duplicates the main logcat window");
            Assert.IsFalse(command.Contains("shell input "),
                $"'{entry.name}' duplicates the input simulation window");
            Assert.IsFalse(command.Contains("screencap"),
                $"'{entry.name}' duplicates the screen capture window");
            Assert.IsFalse(command.Contains("screenrecord"),
                $"'{entry.name}' duplicates the screen recording window");
        }
    }

    [Test]
    public void Catalog_PlaceholdersAreWellFormed()
    {
        foreach (var entry in AndroidLogcatAdbCommandCatalog.All)
        {
            Assert.AreEqual(
                entry.command.Count(c => c == '<'),
                entry.command.Count(c => c == '>'),
                $"Unbalanced placeholder brackets in '{entry.name}'");

            foreach (var placeholder in AndroidLogcatCommandPlaceholders.Parse(entry.command))
                Assert.IsFalse(string.IsNullOrWhiteSpace(placeholder.Token),
                    $"Empty placeholder token in '{entry.name}'");
        }
    }

    [Test]
    public void Catalog_EveryPopulatedCategoryHasEntries()
    {
        var categories = AndroidLogcatAdbCommandCatalog.All.Select(e => e.category).Distinct();

        foreach (var category in categories)
            Assert.IsNotEmpty(
                AndroidLogcatAdbCommandCatalog.All.Where(e => e.category == category).ToArray(),
                $"Category {category} is offered as a chip but has no entries");
    }
}
