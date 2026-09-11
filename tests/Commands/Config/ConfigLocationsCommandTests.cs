using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Config.Revela;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Config;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ConfigLocationsCommandTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InvokeAsync_ConfiguredProject_ShowsAllConfigPathsWithoutCreatingFiles(bool optionalFilesExist)
    {
        using var project = TestProject.CreateMinimal();
        var sitePath = Path.Combine(project.RootPath, "site.json");
        var loggingPath = Path.Combine(project.RootPath, "logging.json");
        if (optionalFilesExist)
        {
            await File.WriteAllTextAsync(sitePath, "{}");
            await File.WriteAllTextAsync(loggingPath, "{}");
        }

        var entries = Directory.GetFileSystemEntries(project.RootPath, "*", SearchOption.AllDirectories);
        Assert.AreNotEqual(Environment.CurrentDirectory, project.RootPath);

        var (exitCode, output) = await InvokeAsync(project.RootPath);

        Assert.AreEqual(0, exitCode);
        AssertProjectLocations(output, project.RootPath);
        AssertGlobalLocations(output);
        CollectionAssert.AreEquivalent(entries, Directory.GetFileSystemEntries(project.RootPath, "*", SearchOption.AllDirectories));
        Assert.AreEqual(optionalFilesExist, File.Exists(sitePath));
        Assert.AreEqual(optionalFilesExist, File.Exists(loggingPath));
    }

    [TestMethod]
    public async Task InvokeAsync_ProjectPathWithMarkupBrackets_DisplaysLiteralPaths()
    {
        using var project = TestProject.CreateMinimal();
        var projectPath = Path.Combine(project.RootPath, "project [draft]");
        Directory.CreateDirectory(projectPath);
        await File.WriteAllTextAsync(Path.Combine(projectPath, "project.json"), "{}");
        var entries = Directory.GetFileSystemEntries(project.RootPath, "*", SearchOption.AllDirectories);

        var (exitCode, output) = await InvokeAsync(projectPath);

        Assert.AreEqual(0, exitCode);
        AssertProjectLocations(output, projectPath);
        CollectionAssert.AreEquivalent(entries, Directory.GetFileSystemEntries(project.RootPath, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InvokeAsync_OutsideProject_ShowsOnlyGlobalLocationsWithoutCreatingFiles(bool projectConfigIsDirectory)
    {
        using var project = TestProject.CreateMinimal();
        var outsidePath = Path.Combine(project.RootPath, "not-a-project");
        Directory.CreateDirectory(outsidePath);
        await File.WriteAllTextAsync(Path.Combine(outsidePath, "site.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(outsidePath, "logging.json"), "{}");
        if (projectConfigIsDirectory)
        {
            Directory.CreateDirectory(Path.Combine(outsidePath, "project.json"));
        }

        var entries = Directory.GetFileSystemEntries(project.RootPath, "*", SearchOption.AllDirectories);

        var (exitCode, output) = await InvokeAsync(outsidePath);

        Assert.AreEqual(0, exitCode);
        AssertNoProjectLocations(output);
        AssertGlobalLocations(output);
        Assert.IsFalse(output.Contains(outsidePath, StringComparison.Ordinal));
        CollectionAssert.AreEquivalent(entries, Directory.GetFileSystemEntries(project.RootPath, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task InvokeAsync_MissingProjectDirectory_ShowsOnlyGlobalLocationsWithoutCreatingDirectory()
    {
        using var project = TestProject.CreateMinimal();
        var missingPath = Path.Combine(project.RootPath, "missing");

        var (exitCode, output) = await InvokeAsync(missingPath);

        Assert.AreEqual(0, exitCode);
        AssertNoProjectLocations(output);
        AssertGlobalLocations(output);
        Assert.IsFalse(Directory.Exists(missingPath));
    }

    [TestMethod]
    public async Task InvokeAsync_EmptyProjectPath_ShowsOnlyGlobalLocations()
    {
        var (exitCode, output) = await InvokeAsync(string.Empty);

        Assert.AreEqual(0, exitCode);
        AssertNoProjectLocations(output);
        AssertGlobalLocations(output);
    }

    private static void AssertProjectLocations(string output, string projectPath)
    {
        StringAssert.Contains(output, "Project Config", StringComparison.Ordinal);
        StringAssert.Contains(output, Path.Combine(projectPath, "project.json"), StringComparison.Ordinal);
        StringAssert.Contains(output, "Site Config", StringComparison.Ordinal);
        StringAssert.Contains(output, Path.Combine(projectPath, "site.json"), StringComparison.Ordinal);
        StringAssert.Contains(output, "Logging Config", StringComparison.Ordinal);
        StringAssert.Contains(output, Path.Combine(projectPath, "logging.json"), StringComparison.Ordinal);
    }

    private static void AssertNoProjectLocations(string output)
    {
        Assert.IsFalse(output.Contains("Project Config", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("Site Config", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("Logging Config", StringComparison.Ordinal));
    }

    private static void AssertGlobalLocations(string output)
    {
        StringAssert.Contains(output, "Installation Type", StringComparison.Ordinal);
        StringAssert.Contains(output, ConfigPathResolver.IsPortableInstallation ? "Portable" : "User", StringComparison.Ordinal);
        StringAssert.Contains(output, "Config Directory", StringComparison.Ordinal);
        StringAssert.Contains(output, ConfigPathResolver.ConfigDirectory, StringComparison.Ordinal);
        StringAssert.Contains(output, "Config File", StringComparison.Ordinal);
        StringAssert.Contains(output, ConfigPathResolver.ConfigFilePath, StringComparison.Ordinal);
        StringAssert.Contains(output, "Plugins (local)", StringComparison.Ordinal);
        StringAssert.Contains(output, ConfigPathResolver.LocalPluginDirectory, StringComparison.Ordinal);
        StringAssert.Contains(output, "Plugins (global)", StringComparison.Ordinal);
        StringAssert.Contains(output, ConfigPathResolver.GlobalPluginDirectory, StringComparison.Ordinal);
        StringAssert.Contains(output, File.Exists(ConfigPathResolver.ConfigFilePath)
            ? "Configuration file exists"
            : "Configuration file not found", StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(string projectPath)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ConfigLocationsCommand>>(NullLogger<ConfigLocationsCommand>.Instance);
        services.AddSingleton(Options.Create(new ProjectEnvironment { Path = projectPath }));
        services.AddTransient<ConfigLocationsCommand>();
        using var provider = services.BuildServiceProvider();
        var command = provider.GetRequiredService<ConfigLocationsCommand>().Create();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 512;
        AnsiConsole.Console = console;

        try
        {
            var parseResult = command.Parse([]);
            Assert.IsEmpty(parseResult.Errors);
            var exitCode = await parseResult.InvokeAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
