using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// End-to-end host behaviour: plugin bootstrap failures and package source creation.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class HostBootstrapTests
{
    [TestMethod]
    public async Task RunAsync_PluginConfigureServicesThrows_SkipsPluginWarnsAndSucceeds()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Broken plugin" } }));
        var broken = new BrokenPlugin();

        var (exitCode, consoleOutput) = await RunWithConsoleAsync(
            () => HostBootstrap.RunAsync(["--version"], new FixedPackageSource(broken), project.RootPath));

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(broken.CommandsRequested, "A plugin that failed to configure must not contribute commands.");
        Assert.Contains(BrokenPlugin.DisplayName, consoleOutput, StringComparison.Ordinal);
        Assert.Contains(BrokenPlugin.FailureReason, consoleOutput, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_DevelopmentEnvironment_PassesEnvironmentToPackageSourceFactory()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Factory" } }));
        string? environmentName = null;

        var exitCode = await HostBootstrap.RunAsync(
            ["--version"],
            (environment, loggerFactory) =>
            {
                environmentName = environment.EnvironmentName;
                Assert.IsNotNull(loggerFactory);
                return new FixedPackageSource();
            },
            project.RootPath);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production", environmentName);
    }

    private static async Task<(int ExitCode, string Output)> RunWithConsoleAsync(Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            var (exitCode, spectreOutput) = await ConsoleCapture.RunAsync(action);
            return (exitCode, spectreOutput + stdout + stderr);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed class FixedPackageSource(params IPlugin[] plugins) : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() =>
            [.. plugins.Select(p => new LoadedPluginInfo(p, PackageSource.Local))];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }

    private sealed class BrokenPlugin : IPlugin
    {
        public const string DisplayName = "Broken Probe";
        public const string FailureReason = "probe failure in ConfigureServices";

        public PackageMetadata Metadata { get; } = new()
        {
            Id = "Tests.BrokenProbe",
            Name = DisplayName,
            Version = "1.0.0",
            Description = "Test fixture",
        };

        public bool CommandsRequested { get; private set; }

        public void ConfigureServices(IServiceCollection services) =>
            throw new InvalidOperationException(FailureReason);

        public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
        {
            CommandsRequested = true;
            return [new CommandDescriptor(new Command("broken-probe"))];
        }
    }
}
