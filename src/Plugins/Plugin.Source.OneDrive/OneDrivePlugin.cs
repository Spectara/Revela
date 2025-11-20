using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Plugin.Source.OneDrive.Commands;

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
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Typed HttpClient for SharedLinkProvider
        services.AddHttpClient<Providers.SharedLinkProvider>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // OneDrive API can be slow for large files
            client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0 (Static Site Generator)");
        });
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services) => this.services = services;

    /// <inheritdoc />
    public IEnumerable<Command> GetCommands()
    {
        if (services == null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call Initialize() first.");
        }

        // Plugin returns ONLY its own command - Program.cs handles parent command
        var oneDriveCommand = new Command("onedrive", "OneDrive source plugin");

        // Add init and download subcommands
        oneDriveCommand.Subcommands.Add(OneDriveInitCommand.Create());
        oneDriveCommand.Subcommands.Add(OneDriveSourceCommand.Create(services));

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
