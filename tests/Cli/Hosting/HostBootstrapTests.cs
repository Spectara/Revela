using System.CommandLine;
using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// End-to-end host behaviour: exit codes, plugin bootstrap failures and host disposal.
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

        Assert.AreEqual(ExitCodes.Success, exitCode);
        Assert.IsFalse(broken.CommandsRequested, "A plugin that failed to configure must not contribute commands.");
        Assert.Contains(BrokenPlugin.DisplayName, consoleOutput, StringComparison.Ordinal);
        Assert.Contains(BrokenPlugin.FailureReason, consoleOutput, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RunAsync_CommandCompleted_DisposesHost()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Dispose" } }));
        var probe = new DisposalProbe();

        var exitCode = await HostBootstrap.RunAsync(
            ["--version"],
            new FixedPackageSource(),
            project.RootPath,
            builder => builder.Services.AddSingleton<IBuildInfo>(_ => new DisposableBuildInfo(probe)));

        Assert.AreEqual(ExitCodes.Success, exitCode);
        Assert.IsTrue(probe.Disposed, "Singletons (and logger providers) are only flushed when the host is disposed.");
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

        Assert.AreEqual(ExitCodes.Success, exitCode);
        Assert.AreEqual(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production", environmentName);
    }

    [TestMethod]
    public async Task RunRevelaAsync_CtrlCDuringDirectCommand_ReturnsCancelledExitCode()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Cancel" } }));
        CommandCancellation? cancellation = null;
        var plugin = new WaitingPlugin(() => cancellation!.Request(PosixSignal.SIGINT));
        var builder = HostBootstrap.CreateBuilder([WaitingPlugin.CommandName], new FixedPackageSource(plugin), project.RootPath);
        using var host = builder.Build();

        var (exitCode, _) = await ConsoleCapture.RunAsync(() => host.RunRevelaAsync(
            [WaitingPlugin.CommandName],
            token => cancellation = CommandCancellation.CreateUnregistered(token)));

        Assert.AreEqual(ExitCodes.Cancelled, exitCode);
        Assert.IsTrue(plugin.StoppedGracefully);
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

    private sealed class WaitingPlugin(Action onStarted) : IPlugin
    {
        public const string CommandName = "wait-probe";

        public PackageMetadata Metadata { get; } = new()
        {
            Id = "Tests.WaitProbe",
            Name = "Wait Probe",
            Version = "1.0.0",
            Description = "Test fixture",
        };

        public bool StoppedGracefully { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
        {
            var command = new Command(CommandName);
            command.SetAction(async (_, token) =>
            {
                onStarted();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    StoppedGracefully = true;
                }

                return ExitCodes.Success;
            });

            return [new CommandDescriptor(command)];
        }
    }

    private sealed class DisposalProbe
    {
        public bool Disposed { get; set; }
    }

    private sealed class DisposableBuildInfo(DisposalProbe probe) : IBuildInfo, IDisposable
    {
        public HostKind Kind => HostKind.Full;

        public string Version => "0.0.0";

        public string InformationalVersion => "0.0.0+probe";

        public string Framework => ".NET";

        public string Configuration => "Debug";

        public string RuntimeIdentifier => "any";

        public string FormatVersionLine() => "revela probe";

        public void Dispose() => probe.Disposed = true;
    }
}
