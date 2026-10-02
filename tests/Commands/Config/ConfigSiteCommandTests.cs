using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace Spectara.Revela.Tests.Commands.Config;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ConfigSiteCommandTests
{
    private const string ThemeName = "TestTheme";

    private const string Template = /*lang=json,strict*/ """
        { "title": "", "author": "", "description": "", "copyright": "" }
        """;

    [TestMethod]
    public async Task ExecuteAsync_EditExisting_WritesSiteJsonAndReloadsConfiguration()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { theme = new { name = ThemeName } }));
        var sitePath = Path.Combine(project.RootPath, "site.json");
        await File.WriteAllTextAsync(sitePath, /*lang=json,strict*/ """{ "title": "Old" }""");
        using var host = BuildHost(project.RootPath);
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        Assert.AreEqual("Old", configuration["site:title"]);

        var (exitCode, _) = await RunAsync(host, "New\r");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("New", JsonNode.Parse(await File.ReadAllTextAsync(sitePath))?["title"]?.GetValue<string>());
        Assert.AreEqual("New", configuration["site:title"], "Later steps in the same process must see the new site.json.");
        Assert.IsEmpty(Directory.GetFiles(project.RootPath, ".site.json.*.tmp"));
    }

    [TestMethod]
    public async Task ExecuteAsync_EditExisting_PreservesKeysMissingFromTemplate()
    {
        // e.g. Lumina's template has no "language"; editing must not silently drop it.
        using var project = TestProject.Create(p => p.WithProjectJson(new { theme = new { name = ThemeName } }));
        var sitePath = Path.Combine(project.RootPath, "site.json");
        await File.WriteAllTextAsync(sitePath, /*lang=json,strict*/ """
            { "title": "Old", "language": "de", "custom": { "flag": true, "list": [1, 2] } }
            """);
        using var host = BuildHost(project.RootPath);

        var (exitCode, _) = await RunAsync(host, string.Empty);

        Assert.AreEqual(0, exitCode);
        var saved = JsonNode.Parse(await File.ReadAllTextAsync(sitePath))!.AsObject();
        Assert.AreEqual("Old", saved["title"]?.GetValue<string>());
        Assert.AreEqual("de", saved["language"]?.GetValue<string>());
        Assert.IsTrue(saved["custom"]?["flag"]?.GetValue<bool>());
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse("[1, 2]"), saved["custom"]?["list"]));
        Assert.IsTrue(saved.ContainsKey("author"), "Template keys missing from the file are added.");
        Assert.AreEqual("de", host.Services.GetRequiredService<IConfiguration>()["site:language"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThemeNotInstalled_PointsToThemeInstall()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { theme = new { name = "Missing[Theme]" } }));
        using var host = BuildHost(project.RootPath, themeInstalled: false);

        var (exitCode, output) = await RunAsync(host, string.Empty);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela theme install Missing[Theme]", output);
        Assert.DoesNotContain("plugin install", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonInteractiveConsole_FailsWithoutPromptingOrWriting()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { theme = new { name = ThemeName } }));
        var sitePath = Path.Combine(project.RootPath, "site.json");
        using var host = BuildHost(project.RootPath, interactive: false);

        var (exitCode, output) = await RunAsync(host, string.Empty);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("interactive", output, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(File.Exists(sitePath));
    }

    private static IHost BuildHost(string projectRoot, bool interactive = true, bool themeInstalled = true)
    {
        var theme = Substitute.For<ITheme>();
        theme.Metadata.Returns(new PackageMetadata
        {
            Id = "Test.Themes." + ThemeName,
            Name = ThemeName,
            Version = "1.0.0",
            Description = "Test theme",
        });
        theme.GetSiteTemplate().Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(Template)));
        var registry = Substitute.For<IThemeRegistry>();
        registry.GetAvailableThemes(Arg.Any<string>()).Returns(themeInstalled ? [theme] : []);
        var capabilities = Substitute.For<IConsoleCapabilities>();
        capabilities.IsInteractive.Returns(interactive);

        return RevelaTestHost.Build(projectRoot, services =>
        {
            services.AddRevelaCommands();
            services.AddSingleton(registry);
            services.AddSingleton(capabilities);
        });
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(IHost host, string input)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var inner = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(writer),
        });
        inner.Profile.Width = 200;
        var keys = input.Select(c => c == '\r'
            ? new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)
            : new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false));
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = new ScriptedConsole(inner, keys);

        try
        {
            var command = host.Services.GetRequiredService<ConfigSiteCommand>();
            var exitCode = await command.ExecuteAsync(CancellationToken.None);
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    /// <summary>
    /// Console that renders through a real Spectre console; prompts read scripted keys and
    /// then Enter (accept default) once the script is exhausted.
    /// </summary>
    private sealed class ScriptedConsole(IAnsiConsole inner, IEnumerable<ConsoleKeyInfo> keys) : IAnsiConsole, IAnsiConsoleInput
    {
        private readonly Queue<ConsoleKeyInfo> pending = new(keys);

        public Profile Profile => inner.Profile;

        public IAnsiConsoleCursor Cursor => inner.Cursor;

        public IAnsiConsoleInput Input => this;

        public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;

        public RenderPipeline Pipeline => inner.Pipeline;

        public void Clear(bool home) => inner.Clear(home);

        public void Write(IRenderable renderable) => inner.Write(renderable);

        public void WriteAnsi(Action<AnsiWriter> action) => inner.WriteAnsi(action);

        public bool IsKeyAvailable() => true;

        public ConsoleKeyInfo? ReadKey(bool intercept) =>
            pending.TryDequeue(out var key) ? key : new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) =>
            Task.FromResult(ReadKey(intercept));
    }
}
