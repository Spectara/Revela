using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Shared host configuration for all CLI entry points.
/// </summary>
/// <remarks>
/// <para>
/// Extracts the common setup logic used by both <c>Cli/Program.cs</c> (dynamic plugin loading)
/// and <c>Cli.Embedded/Program.cs</c> (static plugin references).
/// </para>
/// <para>
/// The only difference between entry points is the <see cref="IPackageSource"/> implementation:
/// <list type="bullet">
/// <item><b>DiskPackageSource</b> — discovers plugins from disk at runtime</item>
/// <item><b>EmbeddedPackageSource</b> — returns statically referenced plugins (AOT-compatible)</item>
/// </list>
/// </para>
/// </remarks>
internal static class HostBootstrap
{
    /// <summary>
    /// Configures the Revela host with all services, configuration, and commands.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="args">CLI arguments.</param>
    /// <param name="packageSource">Source for loading plugins and themes.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder ConfigureRevela(
        this HostApplicationBuilder builder,
        string[] args,
        IPackageSource packageSource)
    {
        // Enable UTF-8 output for proper Unicode/emoji rendering
        Console.OutputEncoding = Encoding.UTF8;

        // Pre-build: Load configuration and register services
        builder.AddRevelaConfiguration();
        builder.Services.AddRevelaConfigSections();
        builder.Services.AddCoreServices();
        builder.Services.AddRevelaCommands();
        builder.Services.AddInteractiveMode();
        builder.Services.AddPackages(packageSource, builder.Configuration, args);

        // Build identity (HostKind, Version, Framework, ...) — single source
        // of truth for `--version` and `revela info`. Idempotent registration
        // so tests can override.
        builder.Services.TryAddSingleton<IBuildInfo, BuildInfo>();

        // Console capabilities — single source of truth for "is this an
        // interactive terminal?". Consumed by the interactive-menu launch and
        // by any command/plugin that renders live output. Idempotent so tests
        // can substitute a fake.
        builder.Services.TryAddSingleton<IConsoleCapabilities, ConsoleCapabilities>();

        // Register ProjectEnvironment (runtime info about project location)
        builder.Services.AddOptions<ProjectEnvironment>()
            .Configure<IHostEnvironment>((env, host) => env.Path = host.ContentRootPath);

        return builder;
    }

    /// <summary>
    /// Runs the Revela CLI from a built host.
    /// </summary>
    /// <param name="host">The built host with all services registered.</param>
    /// <param name="args">CLI arguments to parse and execute.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunRevelaAsync(this IHost host, string[] args)
    {
        var rootCommand = host.UseRevelaCommands();

        // Replace System.CommandLine's default --version action with one that
        // prints the human-readable, host-kind-aware identifier. Same string
        // is used as the first line of `revela info`.
        var buildInfo = host.Services.GetRequiredService<IBuildInfo>();
        var versionOption = rootCommand.Options.OfType<VersionOption>().FirstOrDefault();
        versionOption?.Action = new BuildInfoVersionAction(buildInfo);

        // Detect interactive mode: no arguments AND an interactive terminal.
        var consoleCapabilities = host.Services.GetRequiredService<IConsoleCapabilities>();
        var isInteractiveMode = args.Length == 0 && consoleCapabilities.IsInteractive;

        // Guard both invocation paths: configuration is validated lazily on first
        // IOptions/IOptionsMonitor access inside a command, so an invalid value
        // (e.g. a stray project.language — see #75) surfaces as an
        // OptionsValidationException here. Render it as a clean, styled panel with
        // no stack trace and exit with code 2 instead of crashing.
        try
        {
            if (isInteractiveMode)
            {
                var interactiveService = host.Services.GetRequiredService<IInteractiveMenuService>();
                interactiveService.RootCommand = rootCommand;
                return await interactiveService.RunAsync(CancellationToken.None);
            }

            return await rootCommand.Parse(args).InvokeAsync();
        }
        catch (OptionsValidationException ex)
        {
            ErrorPanels.ShowConfigurationProblem(ex.Failures);
            return 2;
        }
    }

    /// <summary>
    /// Synchronous <c>--version</c> action that prints
    /// <see cref="IBuildInfo.FormatVersionLine"/>.
    /// </summary>
    private sealed class BuildInfoVersionAction(IBuildInfo buildInfo) : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(buildInfo.FormatVersionLine());
            return 0;
        }
    }
}
