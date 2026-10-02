using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Verifies that a malformed Revela configuration file (junk before the JSON in
/// <c>site.json</c> / <c>project.json</c> / …) surfaces as a clean, styled panel with
/// exit code 2 instead of a raw unhandled <see cref="InvalidDataException"/> + stack trace.
/// </summary>
/// <remarks>
/// Config files load eagerly during host construction (<c>ConfigureRevela</c> →
/// <c>AddRevelaConfiguration</c>), which runs outside the guarded region inside
/// <c>RunRevelaAsync</c>. <see cref="HostBootstrap.RunAsync"/> wraps construction in a single
/// guarded region so build-time parse errors are handled like the validation path.
/// These tests capture the shared <see cref="AnsiConsole.Console"/>, so they must not run
/// in parallel with each other.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class MalformedConfigTests
{
    [TestMethod]
    public async Task RunAsync_MalformedSiteJson_ExitsWithCode2AndFriendlyPanel()
    {
        // Arrange: valid project.json, but site.json has raw junk before the JSON.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Test Portfolio" } }));
        await File.WriteAllTextAsync(
            Path.Combine(project.RootPath, "site.json"),
            "this is not json { \"title\": \"Test\" }");

        // Act
        var (exitCode, output) = await RunAsync(project.RootPath, ["check"]);

        // Assert: config-error exit code, friendly panel naming the file, no raw dump.
        Assert.AreEqual(2, exitCode);
        Assert.Contains("Configuration problem", output, StringComparison.Ordinal);
        Assert.Contains("site.json", output, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_MalformedProjectJson_ExitsWithCode2AndFriendlyPanel()
    {
        // Arrange: project.json itself is malformed (junk before the JSON).
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, "%%% not json %%%\n{ }");

        // Act
        var (exitCode, output) = await RunAsync(project.RootPath, ["check"]);

        // Assert
        Assert.AreEqual(2, exitCode);
        Assert.Contains("Configuration problem", output, StringComparison.Ordinal);
        Assert.Contains("project.json", output, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins the framework behaviour the <c>when</c> filter in
    /// <see cref="HostBootstrap.RunAsync"/> depends on: a malformed JSON config source throws
    /// an <see cref="InvalidDataException"/> whose base exception is a
    /// <see cref="JsonException"/> carrying a non-null line number. Guards against a future
    /// framework change silently defeating the friendly-panel path.
    /// </summary>
    [TestMethod]
    public void ConfigurationManager_MalformedSiteJson_ThrowsInvalidDataWithJsonBaseException()
    {
        // Arrange
        var path = Path.Combine(
            Path.GetTempPath(),
            $"revela-badjson-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "garbage before json\n{ \"title\": \"Test\" }");

        try
        {
            // Act
            var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
                new ConfigurationManager()
                    .AddSiteJson(path, optional: false));

            // Assert
            var baseException = ex.GetBaseException();
            Assert.IsInstanceOfType<JsonException>(baseException);
            Assert.IsNotNull(((JsonException)baseException).LineNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string projectPath, string[] args)
    {
        var originalConsole = AnsiConsole.Console;
        var writer = new StringWriter();
        var testConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });

        // Widen the profile so the (long) temp path in the panel isn't word-wrapped,
        // keeping the file-name assertion robust.
        testConsole.Profile.Width = 240;
        AnsiConsole.Console = testConsole;

        try
        {
            var exitCode = await HostBootstrap.RunAsync(args, new EmptyPackageSource(), projectPath);
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class EmptyPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() => [];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}
