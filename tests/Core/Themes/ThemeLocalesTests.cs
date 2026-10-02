using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Core.Themes;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;
using Spectara.Revela.Themes.Lumina.Statistics;

namespace Spectara.Revela.Tests.Core.Themes;

[TestClass]
[TestCategory("Unit")]
public sealed class ThemeLocalesTests
{
    private const string ThemeName = "Lumina";

    [TestMethod]
    [DataRow("de-CH", "de-CH,de,en")]
    [DataRow("de", "de,en")]
    [DataRow("en", "en")]
    [DataRow("EN-us", "EN-us,EN")]
    [DataRow("de_AT", "de-AT,de,en")]
    [DataRow("", "en")]
    [DataRow(null, "en")]
    public void GetLanguageChain_Language_ReturnsExactThenNeutralThenEnglish(string? language, string expected) =>
        Assert.AreEqual(expected, string.Join(',', ThemeLocales.GetLanguageChain(language)));

    [TestMethod]
    public void Translate_RegionalLanguage_FallsBackToNeutralThenEnglishThenKey()
    {
        var theme = CreateTheme(null, new()
        {
            ["Locales/en.json"] = /*lang=json,strict*/ """{ "a": "A-en", "b": "B-en", "c": "C-en" }""",
            ["Locales/de.json"] = /*lang=json,strict*/ """{ "a": "A-de", "b": "B-de" }""",
            ["Locales/de-CH.json"] = /*lang=json,strict*/ """{ "a": "A-ch" }""",
        });
        using var project = TestProject.Create();

        var strings = ThemeLocales.Load(theme, [], project.RootPath, "de-CH", NullLogger.Instance);

        Assert.AreEqual("A-ch", strings.Translate("a", []));
        Assert.AreEqual("B-de", strings.Translate("b", []));
        Assert.AreEqual("C-en", strings.Translate("c", []));
        Assert.AreEqual("missing.key", strings.Translate("missing.key", []));
    }

    [TestMethod]
    public void Translate_MissingKey_LogsDebugOncePerKey()
    {
        var theme = CreateTheme(null, new() { ["Locales/en.json"] = /*lang=json,strict*/ """{ "a": "A" }""" });
        using var project = TestProject.Create();
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        var strings = ThemeLocales.Load(theme, [], project.RootPath, "de", logger);

        strings.Translate("a", []);
        strings.Translate("a", []);
        strings.Translate("gone", []);

        var debugCalls = logger.ReceivedCalls().Count(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)call.GetArguments()[0]! == LogLevel.Debug);
        Assert.AreEqual(2, debugCalls);
    }

    [TestMethod]
    [DataRow("Return to {0}", "Return to Iceland")]
    [DataRow("{1} on {0}", "Canon on 50mm")]
    [DataRow("{0}{0}", "IcelandIceland")]
    [DataRow("{2} {x} {", "{2} {x} {")]
    public void Translate_WithArguments_ReplacesPositionalPlaceholders(string template, string expected)
    {
        var strings = CreateStrings(template);

        var arguments = template.Contains("{1}", StringComparison.Ordinal) ? new object?[] { "50mm", "Canon" } : ["Iceland"];

        Assert.AreEqual(expected, strings.Translate("key", arguments));
    }

    [TestMethod]
    public void Translate_ArgumentContainingPlaceholder_IsNotExpandedAgain()
    {
        var strings = CreateStrings("{0} / {1}");

        Assert.AreEqual("{1} / x", strings.Translate("key", ["{1}", "x"]));
    }

    [TestMethod]
    public void Translate_NumericArgument_IsCultureInvariant()
    {
        var strings = CreateStrings("{0} photos");

        Assert.AreEqual("1.5 photos", strings.Translate("key", [1.5]));
    }

    [TestMethod]
    public void Load_ExtensionAndLocalFiles_OverrideInOrderBaseExtensionLocal()
    {
        var theme = CreateTheme(null, new()
        {
            ["Locales/de.json"] = /*lang=json,strict*/ """{ "base": "theme", "ext": "theme", "local": "theme", "localext": "theme" }""",
        });
        var extension = CreateTheme("statistics", new()
        {
            ["Locales/de.json"] = /*lang=json,strict*/ """{ "ext": "extension", "local": "extension", "localext": "extension" }""",
        });
        using var project = TestProject.Create();
        var localFolder = Path.Combine(project.RootPath, ProjectPaths.Themes, ThemeName, "Locales");
        Directory.CreateDirectory(Path.Combine(localFolder, "Statistics"));
        File.WriteAllText(Path.Combine(localFolder, "de.json"), /*lang=json,strict*/ """{ "local": "local", "localext": "local" }""");
        File.WriteAllText(Path.Combine(localFolder, "Statistics", "de.json"), /*lang=json,strict*/ """{ "localext": "local-extension" }""");

        var strings = ThemeLocales.Load(theme, [extension], project.RootPath, "de", NullLogger.Instance);

        Assert.AreEqual("theme", strings.Translate("base", []));
        Assert.AreEqual("extension", strings.Translate("ext", []));
        Assert.AreEqual("local", strings.Translate("local", []));
        Assert.AreEqual("local-extension", strings.Translate("localext", []));
    }

    [TestMethod]
    public void Load_InvalidLocalFile_ThrowsWithFileName()
    {
        var theme = CreateTheme(null, []);
        using var project = TestProject.Create();
        var localFolder = Path.Combine(project.RootPath, ProjectPaths.Themes, ThemeName, "Locales");
        Directory.CreateDirectory(localFolder);
        File.WriteAllText(Path.Combine(localFolder, "en.json"), /*lang=json,strict*/ """{ "photo.close": 42 }""");

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => ThemeLocales.Load(theme, [], project.RootPath, "en", NullLogger.Instance));

        Assert.Contains("en.json", exception.Message);
        Assert.Contains("photo.close", exception.Message);
    }

    [TestMethod]
    [DataRow("de", "Januar")]
    [DataRow("en", "January")]
    [DataRow("xx-unknown", "January")]
    public void Load_Language_ResolvesFormattingCulture(string language, string expectedMonth)
    {
        using var project = TestProject.Create();

        var strings = ThemeLocales.Load(CreateTheme(null, []), [], project.RootPath, language, NullLogger.Instance);

        Assert.AreEqual(expectedMonth, new DateTime(2024, 1, 20).ToString("MMMM", strings.Culture));
    }

    [TestMethod]
    public void GetEntries_ThemeAndExtension_UsesDistinctKeysAndOverridePaths()
    {
        var theme = CreateTheme(null, new() { ["Locales/de.json"] = "{}", ["Configuration/images.json"] = "{}" });
        var extension = CreateTheme("statistics", new() { ["Locales/de.json"] = "{}" });

        var entries = ThemeLocales.GetEntries(theme, [extension]);

        Assert.HasCount(2, entries);
        var baseEntry = entries.Single(e => e.Source == FileSourceType.Theme);
        var extensionEntry = entries.Single(e => e.Source == FileSourceType.Extension);
        Assert.AreEqual("locales/de.json", baseEntry.Key);
        Assert.AreEqual("locales/statistics/de.json", extensionEntry.Key);
        Assert.AreEqual("Locales/de.json", extensionEntry.OriginalPath);
        Assert.AreEqual(Path.Combine("Locales", "de.json"), ThemeLocales.GetLocalPath(baseEntry.Key));
        Assert.AreEqual(Path.Combine("Locales", "Statistics", "de.json"), ThemeLocales.GetLocalPath(extensionEntry.Key));
    }

    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void EmbeddedLuminaThemes_ShipCompleteEnglishAndGermanLocales(string language)
    {
        using var project = TestProject.Create();
        ITheme[] themes = [new LuminaTheme(), new LuminaStatisticsExtension()];

        foreach (var theme in themes)
        {
            var englishKeys = ReadKeys(theme, "en");
            var keys = ReadKeys(theme, language);

            Assert.IsNotEmpty(englishKeys, $"{theme.Metadata.Name} ships no Locales/en.json");
            CollectionAssert.AreEquivalent(englishKeys, keys, $"{theme.Metadata.Name} Locales/{language}.json keys differ from en.json");
        }
    }

    private static List<string> ReadKeys(ITheme theme, string language)
    {
        using var stream = theme.GetFile($"Locales/{language}.json");
        Assert.IsNotNull(stream, $"{theme.Metadata.Name} is missing Locales/{language}.json");
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }

    private static ThemeStrings CreateStrings(string text) =>
        new(
            "en",
            [("en", new Dictionary<string, string> { ["key"] = text })],
            System.Globalization.CultureInfo.InvariantCulture,
            NullLogger.Instance);

    private static ITheme CreateTheme(string? prefix, Dictionary<string, string> files)
    {
        var theme = Substitute.For<ITheme>();
        var name = prefix is null ? ThemeName : $"{ThemeName} {prefix}";
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = "Test." + name.Replace(' ', '.'),
            Name = name,
            Version = "1.0.0",
            Description = "Test",
            Author = "Test"
        });
        theme.Prefix.Returns(prefix);
        theme.GetAllFiles().Returns([.. files.Keys.Select(path => path.Replace('/', Path.DirectorySeparatorChar))]);
        theme.GetFile(Arg.Any<string>()).Returns(call =>
        {
            var path = call.Arg<string>().Replace('\\', '/');
            return files.TryGetValue(path, out var content) ? new MemoryStream(Encoding.UTF8.GetBytes(content)) : null;
        });
        return theme;
    }
}
