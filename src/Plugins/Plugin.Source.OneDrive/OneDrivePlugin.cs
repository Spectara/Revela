using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Plugin.Source.OneDrive.Commands;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;

namespace Spectara.Revela.Plugin.Source.OneDrive;

/// <summary>
/// OneDrive source plugin for Revela
/// </summary>
public sealed class OneDrivePlugin : IPlugin
{
    private IServiceProvider? services;

    /// <inheritdoc />
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "OneDrive Source",
        Version = "1.0.0",
        Description = "Download images from OneDrive shared folders",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Register plugin-specific configuration file
        // This allows users to create onedrive.json for plugin-specific settings
        configuration.AddJsonFile(
            "onedrive.json",
            optional: true,          // File doesn't need to exist
            reloadOnChange: true     // Support hot reload
        );

        // Plugin can also add environment variable prefix (in addition to global REVELA__)
        // Note: This is optional - global REVELA__PLUGINS__ONEDRIVE__* already works
        configuration.AddEnvironmentVariables(prefix: "ONEDRIVE_");
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration (IOptions pattern)
        // Binds to Plugins:OneDrive section from all registered config sources
        services.AddOptions<OneDrivePluginConfig>()
            .BindConfiguration(OneDrivePluginConfig.SectionName)
            .ValidateDataAnnotations()      // Validate [Required], [Url], etc.
            .ValidateOnStart();             // Fail-fast at startup if config invalid

        // Register Typed HttpClient for SharedLinkProvider with Resilience
        // Standard resilience handler provides: retry (3x), circuit breaker, timeout, rate limiter
        // Handles: HTTP 408, 429, 500+ (including 503), HttpRequestException, TimeoutRejectedException
        services.AddHttpClient<Providers.SharedLinkProvider>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // OneDrive API can be slow for large files
            client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0 (Static Site Generator)");
        })
        .AddStandardResilienceHandler(); // Modern .NET 10 resilience (replaces old Polly)

        // Register Services
        services.AddSingleton<Services.DownloadAnalyzer>();

        // Register Commands for Dependency Injection
        services.AddTransient<OneDriveInitCommand>();
        services.AddTransient<OneDriveSourceCommand>();
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services) => this.services = services;

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        if (services is null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call Initialize() first.");
        }

        // Resolve commands from DI container
        var initCommand = services.GetRequiredService<OneDriveInitCommand>();
        var sourceCommand = services.GetRequiredService<OneDriveSourceCommand>();

        // 1. Register init command → revela init source onedrive
        //    Creates: init → source → onedrive (nested parent)
        yield return new CommandDescriptor(initCommand.Create(), ParentCommand: "init source");

        // 2. Register source command → revela source onedrive sync
        //    Creates: source → onedrive → sync
        var oneDriveCommand = new Command("onedrive", "OneDrive shared folder source");
        oneDriveCommand.Subcommands.Add(sourceCommand.Create());
        yield return new CommandDescriptor(oneDriveCommand, ParentCommand: "source");
    }
}

/// <summary>
/// Plugin metadata implementation
/// </summary>
internal sealed class PluginMetadata : IPluginMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
}

