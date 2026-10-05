using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Plugins.Serve;
using Spectara.Revela.Plugins.Source.Calendar;
using Spectara.Revela.Plugins.Source.OneDrive;
using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// End-to-end checks that plugin assemblies publish their <c>plugins:&lt;key&gt;</c>
/// claims (via the SDK source generator) and that the host rejects duplicate claims.
/// </summary>
/// <remarks>
/// This test assembly declares <see cref="ForeignServeConfig"/> with the section
/// <c>plugins:serve</c>, so the generator makes it claim <c>serve</c> — exactly what a
/// third-party plugin reusing an official key would do.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PluginConfigOwnershipHostTests
{
    [TestMethod]
    public void FromPackages_OfficialPlugins_ClaimsDocumentedKeys()
    {
        var ownership = PluginConfigOwnership.FromPackages(
        [
            new ServePlugin(),
            new StatisticsPlugin(),
            new OneDrivePlugin(),
            new SourceCalendarPlugin(),
        ]);

        Assert.HasCount(4, ownership.Owners);
        Assert.AreEqual("Spectara.Revela.Plugins.Serve", ownership.Owners["serve"].PackageId);
        Assert.AreEqual("Spectara.Revela.Plugins.Statistics", ownership.Owners["statistics"].PackageId);
        Assert.AreEqual("Spectara.Revela.Plugins.Source.OneDrive", ownership.Owners["oneDrive"].PackageId);
        Assert.AreEqual("Spectara.Revela.Plugins.Source.Calendar", ownership.Owners["calendarFeeds"].PackageId);
        Assert.AreSame(typeof(ServePlugin).Assembly, ownership.Owners["serve"].Assembly);
        Assert.AreSame(typeof(OneDrivePlugin).Assembly, ownership.Owners["oneDrive"].Assembly);
    }

    [TestMethod]
    public async Task RunAsync_TwoPackagesClaimSameKey_FailsBeforeConfiguringServicesAndNamesBothPackages()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Conflict" } }));
        var foreignPlugin = new ForeignServePlugin();
        var source = new ConflictingPluginSource(foreignPlugin);

        var (exitCode, output) = await RunCliAsync(project.RootPath, source);

        Assert.AreEqual(ExitCodes.ConfigurationProblem, exitCode);
        Assert.Contains("Spectara.Revela.Plugins.Serve", output, StringComparison.Ordinal);
        Assert.Contains(ForeignServePlugin.PackageId, output, StringComparison.Ordinal);
        Assert.Contains("plugins:serve", output, StringComparison.Ordinal);
        Assert.IsFalse(foreignPlugin.ServicesConfigured);
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(string projectPath, IPackageSource source)
    {
        var originalConsole = AnsiConsole.Console;
        var writer = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });

        try
        {
            var exitCode = await HostBootstrap.RunAsync(["--version"], source, projectPath);
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class ConflictingPluginSource(ForeignServePlugin foreignPlugin) : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() =>
        [
            new(new ServePlugin(), PackageSource.Bundled),
            new(foreignPlugin, PackageSource.Local),
        ];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}

/// <summary>
/// Config class whose section makes this test assembly claim <c>plugins:serve</c>.
/// </summary>
[RevelaConfig("plugins:serve")]
internal sealed class ForeignServeConfig
{
    public const string Section = "plugins:serve";

    public int Port { get; set; }
}

/// <summary>
/// A third-party plugin that (wrongly) reuses the official <c>serve</c> key.
/// </summary>
internal sealed class ForeignServePlugin : IPlugin
{
    public const string PackageId = "Contoso.Revela.Plugins.Serve";

    public PackageMetadata Metadata { get; } = new()
    {
        Id = PackageId,
        Name = "Contoso Serve",
        Version = "1.0.0",
        Description = "Test plugin claiming an already claimed key",
    };

    public bool ServicesConfigured { get; private set; }

    public void ConfigureServices(IServiceCollection services)
    {
        ServicesConfigured = true;
        services.AddOptions<ForeignServeConfig>().BindConfiguration(ForeignServeConfig.Section);
    }
}
