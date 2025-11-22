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
        Author = "Spectara",
        ParentCommand = "source" // Plugin declares it wants to be under 'source' parent
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

        // Register Typed HttpClient for SharedLinkProvider
        services.AddHttpClient<Providers.SharedLinkProvider>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // OneDrive API can be slow for large files
            client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0 (Static Site Generator)");
        });

        // Register Commands for Dependency Injection
        services.AddTransient<OneDriveInitCommand>();
        services.AddTransient<OneDriveSourceCommand>();
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services) => this.services = services;

    /// <inheritdoc />
    public IEnumerable<Command> GetCommands()
    {
        if (services is null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call Initialize() first.");
        }

        // Plugin returns ONLY its own command - Program.cs handles parent command
        var oneDriveCommand = new Command("onedrive", "OneDrive source plugin");

        // Resolve commands from DI container (Modern DI pattern)
        var initCommand = services.GetRequiredService<OneDriveInitCommand>();
        var sourceCommand = services.GetRequiredService<OneDriveSourceCommand>();

        // Add init and download subcommands
        oneDriveCommand.Subcommands.Add(initCommand.Create());
        oneDriveCommand.Subcommands.Add(sourceCommand.Create());

        yield return oneDriveCommand;
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
    public string? ParentCommand { get; init; }
}
