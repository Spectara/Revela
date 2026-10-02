using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Verifies that the real host bootstrap applies exactly the documented configuration
/// layers: property defaults → revela.json → project.json → site.json → logging.json →
/// <c>SPECTARA__REVELA__</c> environment variables. No implicit host defaults
/// (<c>appsettings*.json</c>, unprefixed environment variables, command-line config).
/// </summary>
/// <remarks>
/// Mutates process-wide environment variables, so the tests must not run in parallel.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ConfigurationLayerTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("theme install Spectara.Revela.Themes.Lumina")]
    [DataRow("plugin uninstall Spectara.Revela.Plugins.Serve")]
    public void CreateBuilder_PrefixedEnvironmentVariable_OverridesProjectJson(string commandLine)
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "FromProjectJson" } }));

        var name = WithEnvironment("SPECTARA__REVELA__PROJECT__NAME", "FromEnvironment", () =>
        {
            using var host = Build(project.RootPath, commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return host.Services.GetRequiredService<IOptions<ProjectConfig>>().Value.Name;
        });

        Assert.AreEqual("FromEnvironment", name);
    }

    [TestMethod]
    public void CreateBuilder_PrefixedEnvironmentVariable_ReachesPackageManagementCommands()
    {
        // Feeds configured via environment must be visible to install, which skips package loading.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Feeds" } }));

        var feeds = WithEnvironment("SPECTARA__REVELA__DEPENDENCIES__FEEDS__ENVFEED", "https://example.test/feed", () =>
        {
            using var host = Build(project.RootPath, ["plugin", "install", "Some.Plugin"]);
            return host.Services.GetRequiredService<IOptions<DependenciesConfig>>().Value.Feeds;
        });

        Assert.AreEqual("https://example.test/feed", feeds["EnvFeed"]);
    }

    [TestMethod]
    public void CreateBuilder_PrefixedEnvironmentVariable_OverridesSiteJson()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Site" } }));
        File.WriteAllText(Path.Combine(project.RootPath, "site.json"), /*lang=json,strict*/ """{ "title": "FromSiteJson" }""");

        var title = WithEnvironment("SPECTARA__REVELA__SITE__TITLE", "FromEnvironment", () =>
        {
            using var host = Build(project.RootPath, []);
            return host.Services.GetRequiredService<IConfiguration>()["site:title"];
        });

        Assert.AreEqual("FromEnvironment", title);
    }

    [TestMethod]
    public void CreateBuilder_PrefixedEnvironmentLogLevel_OverridesBuiltInDefault()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Logging" } }));

        var debugEnabled = WithEnvironment("SPECTARA__REVELA__LOGGING__LOGLEVEL__Spectara.Revela", "Debug", () =>
        {
            using var host = Build(project.RootPath, []);
            return host.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Spectara.Revela.Tests")
                .IsEnabled(LogLevel.Debug);
        });

        Assert.IsTrue(debugEnabled, "SPECTARA__REVELA__LOGGING__* must override the built-in Warning level.");
    }

    [TestMethod]
    public void CreateBuilder_LoggingJson_OverridesBuiltInDefault()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Logging" } }));
        File.WriteAllText(
            Path.Combine(project.RootPath, "logging.json"),
            /*lang=json,strict*/ """{ "Logging": { "LogLevel": { "Spectara.Revela": "Debug" } } }""");

        using var host = Build(project.RootPath, []);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Spectara.Revela.Tests");

        Assert.IsTrue(logger.IsEnabled(LogLevel.Debug));
    }

    [TestMethod]
    public void CreateBuilder_NoLoggingConfiguration_DefaultsToWarning()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Logging" } }));

        using var host = Build(project.RootPath, []);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Spectara.Revela.Tests");

        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
    }

    [TestMethod]
    [DataRow("appsettings.json")]
    [DataRow("appsettings.Production.json")]
    public void CreateBuilder_AppSettingsInProjectDirectory_IsNotRead(string fileName)
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { theme = new { name = "Lumina" } }));
        File.WriteAllText(
            Path.Combine(project.RootPath, fileName),
            /*lang=json,strict*/ """{ "project": { "name": "FromAppSettings" } }""");

        using var host = Build(project.RootPath, []);

        Assert.IsNull(host.Services.GetRequiredService<IConfiguration>()["project:name"]);
    }

    [TestMethod]
    public void CreateBuilder_UnprefixedEnvironmentVariable_IsNotRead()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { theme = new { name = "Lumina" } }));

        var name = WithEnvironment("PROJECT__NAME", "FromUnprefixedEnvironment", () =>
        {
            using var host = Build(project.RootPath, []);
            return host.Services.GetRequiredService<IConfiguration>()["project:name"];
        });

        Assert.IsNull(name);
    }

    [TestMethod]
    public void CreateBuilder_CommandLineArguments_AreNotConfiguration()
    {
        // CLI arguments belong to System.CommandLine; `--name X` must not become config key "name".
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { theme = new { name = "Lumina" } }));

        using var host = Build(project.RootPath, ["config", "project", "--project:name", "FromArgs", "--name", "Other"]);
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        Assert.IsNull(configuration["project:name"]);
        Assert.IsNull(configuration["name"]);
    }

    [TestMethod]
    public void CreateBuilder_ContentRoot_IsProjectDirectory()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Root" } }));

        using var host = Build(project.RootPath, []);

        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(project.RootPath),
            Path.TrimEndingDirectorySeparator(host.Services.GetRequiredService<IHostEnvironment>().ContentRootPath));
    }

    private static IHost Build(string projectRoot, string[] args) =>
        HostBootstrap.CreateBuilder(args, new EmptyPackageSource(), projectRoot).Build();

    private static T WithEnvironment<T>(string name, string value, Func<T> action)
    {
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private sealed class EmptyPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() => [];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}
