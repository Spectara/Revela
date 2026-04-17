using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

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
    /// <param name="filteredArgs">CLI arguments (after --project removal).</param>
    /// <param name="packageSource">Source for loading plugins and themes.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder ConfigureRevela(
        this HostApplicationBuilder builder,
        string[] filteredArgs,
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
        builder.Services.AddPackages(packageSource, builder.Configuration, filteredArgs);

        // Register ProjectEnvironment (runtime info about project location)
        builder.Services.AddOptions<ProjectEnvironment>()
            .Configure<IHostEnvironment>((env, host) => env.Path = host.ContentRootPath);

        return builder;
    }

    /// <summary>
    /// Runs the Revela CLI from a built host.
    /// </summary>
    /// <param name="host">The built host with all services registered.</param>
    /// <param name="filteredArgs">CLI arguments to parse and execute.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunRevelaAsync(this IHost host, string[] filteredArgs)
    {
        var rootCommand = host.UseRevelaCommands();

        // Detect interactive mode: no arguments AND interactive terminal
        var isInteractiveMode = filteredArgs.Length == 0
            && !Console.IsInputRedirected
            && !Console.IsOutputRedirected
            && Environment.UserInteractive;

        if (isInteractiveMode)
        {
            var interactiveService = host.Services.GetRequiredService<IInteractiveMenuService>();
            interactiveService.RootCommand = rootCommand;
            return await interactiveService.RunAsync(CancellationToken.None);
        }

        return await rootCommand.Parse(filteredArgs).InvokeAsync();
    }
}
