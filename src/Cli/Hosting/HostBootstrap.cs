using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;
using Spectre.Console;

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
    /// Builds and runs the Revela host inside a single guarded region so that a
    /// malformed configuration file surfaces as a friendly panel with exit code 2
    /// instead of a raw unhandled exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration files are added to a <see cref="ConfigurationManager"/>
    /// during <see cref="ConfigureRevela"/>, which loads them <b>eagerly</b>. A syntax error in
    /// <c>revela.json</c>, <c>project.json</c>, <c>site.json</c> or <c>logging.json</c> therefore
    /// throws while the host is still being constructed — <em>before</em> the guarded region inside
    /// <see cref="RunRevelaAsync"/>. Wrapping <c>create builder → ConfigureRevela → build → run</c>
    /// in one <c>try</c> lets both entry points handle build-time and run-time config errors uniformly.
    /// </para>
    /// <para>
    /// The catch is deliberately narrow: only an <see cref="InvalidDataException"/> whose base
    /// exception is a <see cref="JsonException"/> (the shape thrown by the JSON configuration
    /// providers) is turned into a panel. Any other construction error still surfaces as before.
    /// </para>
    /// </remarks>
    /// <param name="args">CLI arguments.</param>
    /// <param name="packageSource">Source for loading plugins and themes.</param>
    /// <param name="contentRootPath">
    /// Project directory used as the host content root. Defaults to
    /// <see cref="Directory.GetCurrentDirectory"/> when <see langword="null"/>.
    /// </param>
    /// <param name="configureExtra">
    /// Optional host-specific configuration applied after <see cref="ConfigureRevela"/> and before
    /// <c>Build()</c> (e.g. the dynamic CLI registers NuGet package management here).
    /// </param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(
        string[] args,
        IPackageSource packageSource,
        string? contentRootPath = null,
        Action<HostApplicationBuilder>? configureExtra = null)
    {
        try
        {
            var builder = CreateBuilder(args, packageSource, contentRootPath);
            configureExtra?.Invoke(builder);

            return await builder.Build().RunRevelaAsync(args);
        }
        catch (PluginConfigConflictException ex)
        {
            ErrorPanels.ShowError("Plugin configuration conflict", Markup.Escape(ex.Message));
            return 1;
        }
        catch (InvalidDataException ex) when (ex.GetBaseException() is JsonException jsonException)
        {
            var path = ExtractConfigFilePath(ex.Message);
            ErrorPanels.ShowConfigFileError(path, jsonException.LineNumber, jsonException.BytePositionInLine);
            return 2;
        }
    }

    /// <summary>
    /// Creates the host builder with the Revela configuration chain and services applied.
    /// </summary>
    /// <param name="args">CLI arguments.</param>
    /// <param name="packageSource">Source for loading plugins and themes.</param>
    /// <param name="contentRootPath">
    /// Project directory used as the host content root. Defaults to
    /// <see cref="Directory.GetCurrentDirectory"/> when <see langword="null"/>.
    /// </param>
    /// <returns>The configured builder, ready to build.</returns>
    internal static HostApplicationBuilder CreateBuilder(
        string[] args,
        IPackageSource packageSource,
        string? contentRootPath = null)
    {
        // No host defaults: they would add appsettings*.json from the project directory,
        // unprefixed environment variables and the CLI arguments as configuration in front of
        // Revela's own layers (see HostBuilderExtensions.AddRevelaConfiguration). Args are
        // deliberately not passed — they belong to System.CommandLine, not to configuration.
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = contentRootPath ?? Directory.GetCurrentDirectory(),
            EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Production,
        });

        // Development (e.g. launchSettings.json) keeps the DI validation the host defaults gave it
        if (builder.Environment.IsDevelopment())
        {
            builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            }));
        }

        builder.ConfigureRevela(args, packageSource);
        return builder;
    }

    /// <summary>
    /// Extracts the file path from the framework message
    /// <c>"Failed to load configuration from file '{path}'."</c> by reading the substring
    /// between the single quotes, falling back to a generic phrase when it can't be found.
    /// </summary>
    private static string ExtractConfigFilePath(string message)
    {
        var start = message.IndexOf('\'', StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = message.IndexOf('\'', start + 1);
            if (end > start + 1)
            {
                return message[(start + 1)..end];
            }
        }

        return "one of your configuration files";
    }

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
        builder.AddRevelaLogging();
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

        // Warn about plugins:<key> settings no loaded plugin claims (typos, uninstalled plugins).
        host.Services.GetService<UnclaimedPluginConfigReporter>()?.Report();

        // Opt out of System.CommandLine's default exception handler so we can turn
        // a configuration validation failure into a friendly panel ourselves.
        // Otherwise it would swallow the exception, print a raw stack trace, and
        // return 1 before our catch below could run.
        var invocationConfiguration = new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false,
        };

        // Guard both invocation paths: configuration is validated lazily on first
        // IOptions/IOptionsMonitor access inside a command, so an invalid value
        // (e.g. a stray project.language — see #75) surfaces as an
        // OptionsValidationException here. Render it as a clean, styled panel with
        // no stack trace and exit with code 2 instead of crashing.
        try
        {
            // No arguments = the interactive menu. It runs outside System.CommandLine's
            // invocation so Ctrl+C only cancels the command started from the menu, and it
            // decides itself (via IConsoleCapabilities) whether the terminal is interactive.
            if (args.Length == 0)
            {
                var interactiveService = host.Services.GetRequiredService<IInteractiveMenuService>();
                interactiveService.RootCommand = rootCommand;
                return await interactiveService.RunAsync(CancellationToken.None);
            }

            return await rootCommand.Parse(args).InvokeAsync(invocationConfiguration);
        }
        catch (OptionsValidationException ex)
        {
            ErrorPanels.ShowConfigurationProblem(ex.Failures);
            return 2;
        }
        catch (OperationCanceledException)
        {
            // Cancellation (Ctrl+C) — nothing to report; mirror the previous exit code.
            return 1;
        }
        catch (Exception ex)
        {
            // Preserve System.CommandLine's former default-handler behaviour for any
            // other unexpected error now that we've opted out of it: write the
            // details to stderr and return exit code 1.
            await Console.Error.WriteLineAsync($"Unhandled exception: {ex}");
            return 1;
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
