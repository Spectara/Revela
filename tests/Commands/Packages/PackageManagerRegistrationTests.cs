using System.CommandLine;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NuGet.Packaging;
using NuGet.Versioning;
using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PackageManagerRegistrationTests
{
    private const string PackageId = "Fixture.Plugin";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PluginInstallCommand_InFlightCancellation_PropagatesWithoutFailureOrGlobalRegistration(bool installAll)
    {
        const string fullPackageId = "Spectara.Revela.Plugins.Fixture";
        var installer = Substitute.For<IPackageInstaller>();
        var installerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var installerToken = CancellationToken.None;
        installer.InstallAsync(fullPackageId, null, null, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            installerToken = call.Arg<CancellationToken>();
            installerEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, installerToken);
            return (InstalledPackage?)new InstalledPackage(fullPackageId, "2.0.0", ["RevelaPlugin"]);
        });
        var indexService = Substitute.For<IPackageIndexService>();
        indexService.SearchByTypeAsync("RevelaPlugin", Arg.Any<CancellationToken>()).Returns(
            [new PackageIndexEntry { Id = fullPackageId, Version = "2.0.0", Description = "Fixture plugin", Source = "fixture", Types = ["RevelaPlugin"] }]);
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        globalConfig.GetPackagesAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, string?>());
        var logger = new RegistrationLogger<PluginInstallCommand>();
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns([]);
        var consent = new ProjectFeedConsent(sourceManager, Substitute.For<IConsoleCapabilities>());
        var command = new PluginInstallCommand(logger, installer, indexService, globalConfig, consent).Create();
        using var cancellation = new CancellationTokenSource();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;
        AnsiConsole.Console = console;
        var execution = Task.FromResult(0);

        try
        {
            var parsed = command.Parse(installAll ? ["--all"] : ["Fixture"]);
            Assert.IsEmpty(parsed.Errors);
            var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false, Output = writer, Error = writer };
            execution = parsed.InvokeAsync(invocation, cancellation.Token);
            await installerEntered.Task.WaitAsync(OperationTimeout);
            Assert.IsFalse(execution.IsCompleted);
            await cancellation.CancelAsync();

            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => execution.WaitAsync(OperationTimeout));

            Assert.AreEqual(installerToken, exception.CancellationToken);
            Assert.IsTrue(exception.CancellationToken.IsCancellationRequested);
            await installer.Received(1).InstallAsync(fullPackageId, null, null, installerToken);
            await globalConfig.DidNotReceive().AddPackageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            Assert.IsFalse(logger.Entries.Any(entry => entry.Level >= LogLevel.Error));
            Assert.DoesNotContain("Failed to install", writer.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("installed successfully", writer.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OperationCanceledException", writer.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("TaskCanceledException", writer.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("0 of 1", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                await execution.WaitAsync(OperationTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                AnsiConsole.Console = originalConsole;
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InstallFromNupkgAsync_RegistrationFails_RetainsFilesWithoutLoggingSuccess(bool cancel)
    {
        using var project = TestProject.Create();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        var nupkgPath = await CreatePackageAsync(project);
        var targetDir = Path.Combine(project.RootPath, "installed");
        using var cancellation = new CancellationTokenSource();
        var failure = cancel
            ? (Exception)new OperationCanceledException("Injected registration cancellation.", cancellation.Token)
            : new IOException("Injected registration write failure.");
        var configService = Substitute.For<IConfigService>();
        configService.IsProjectInitialized().Returns(true);
        configService.UpdateProjectConfigAsync(Arg.Any<JsonObject>(), cancellation.Token).Returns(async _ =>
        {
            if (cancel)
            {
                await cancellation.CancelAsync();
            }

            throw failure;
        });
        var logger = new RegistrationLogger<PackageManager>();
        using var provider = CreateInstallerProvider(configService, logger);
        var installer = provider.GetRequiredService<PackageManager>();

        if (cancel)
        {
            var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => installer.InstallFromNupkgAsync(
                nupkgPath, targetDir, nupkgPath, cancellation.Token));
            Assert.AreSame(failure, thrown);
            Assert.AreEqual(cancellation.Token, thrown.CancellationToken);
        }
        else
        {
            Assert.IsNull(await installer.InstallFromNupkgAsync(nupkgPath, targetDir, nupkgPath, cancellation.Token));
        }

        var installedPath = Path.Combine(targetDir, PackageId);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(installedPath, $"{PackageId}.dll")));
        Assert.IsTrue(File.Exists(Path.Combine(installedPath, $"{PackageId}.meta.json")));
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        await configService.Received(1).UpdateProjectConfigAsync(
            Arg.Is<JsonObject>(patch => patch.Count == 1 && patch["dependencies"]!.AsObject().Count == 1 &&
                patch["dependencies"]!["packages"]!.AsObject().Count == 1 &&
                patch["dependencies"]!["packages"]![PackageId]!.GetValue<string>() == "2.0.0"), cancellation.Token);
        var logs = logger.Entries;
        Assert.IsFalse(logs.Any(entry => entry.Message.Contains("installed successfully", StringComparison.Ordinal)));
        if (cancel)
        {
            Assert.IsEmpty(logs);
        }
        else
        {
            Assert.HasCount(1, logs);
            Assert.AreEqual(LogLevel.Error, logs[0].Level);
            Assert.AreSame(failure, logs[0].Exception);
            Assert.Contains("files extracted but project registration failed", logs[0].Message, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task InstallAndUninstallAsync_PreCanceled_PropagateBeforeAccessingGlobalPaths()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        var logger = Substitute.For<ILogger<PackageManager>>();
        using var provider = CreateInstallerProvider(configService, logger);
        var installer = provider.GetRequiredService<PackageManager>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var installFailure = await Assert.ThrowsAsync<OperationCanceledException>(() => installer.InstallAsync(
            PackageId, cancellationToken: cancellation.Token));
        var uninstallFailure = await Assert.ThrowsAsync<OperationCanceledException>(() => installer.UninstallPluginAsync(
            PackageId, cancellation.Token));

        Assert.AreEqual(cancellation.Token, installFailure.CancellationToken);
        Assert.AreEqual(cancellation.Token, uninstallFailure.CancellationToken);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(project.ProjectJsonPath));
        Assert.IsEmpty(logger.ReceivedCalls());
    }

    private static ServiceProvider CreateInstallerProvider(IConfigService configService, ILogger<PackageManager> logger)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configService);
        services.AddSingleton(logger);
        services.AddSingleton(new NupkgExtractor(NullLogger<NupkgExtractor>.Instance, TimeProvider.System));
        services.AddSingleton<PluginProjectService>();
        services.AddSingleton(Substitute.For<INuGetSourceManager>());
        services.AddSingleton(Substitute.For<IBuildInfo>());
        services.AddHttpClient<PackageManager>();
        return services.BuildServiceProvider();
    }

    private static async Task<string> CreatePackageAsync(TestProject project)
    {
        var libraryPath = Path.Combine(project.RootPath, $"{PackageId}.dll");
        await File.WriteAllBytesAsync(libraryPath, [1, 2, 3, 4]);
        var package = new PackageBuilder
        {
            Id = PackageId,
            Version = new NuGetVersion("2.0.0"),
            Description = "Synthetic registration test package"
        };
        package.Authors.Add("Test");
        package.Files.Add(new PhysicalPackageFile
        {
            SourcePath = libraryPath,
            TargetPath = $"lib/net10.0/{PackageId}.dll"
        });
        var packagePath = Path.Combine(project.RootPath, $"{PackageId}.2.0.0.nupkg");
        using var stream = File.Create(packagePath);
        package.Save(stream);
        return packagePath;
    }

    private sealed class RegistrationLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
