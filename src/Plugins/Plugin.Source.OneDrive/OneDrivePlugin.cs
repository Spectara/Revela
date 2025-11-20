using System.CommandLine;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Plugin.Source.OneDrive.Commands;

namespace Spectara.Revela.Plugin.Source.OneDrive;

/// <summary>
/// OneDrive source plugin for Revela
/// </summary>
public sealed class OneDrivePlugin : IPlugin
{
    private IServiceProvider? _services;

    /// <inheritdoc />
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "OneDrive Source",
        Version = "1.0.0",
        Description = "Download images from OneDrive shared folders",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void Initialize(IServiceProvider services) => _services = services;

    /// <inheritdoc />
    public IEnumerable<Command> GetCommands()
    {
        if (_services == null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call Initialize() first.");
        }

        // Create parent "source" command
        var sourceCommand = new Command("source", "Manage image sources");

        // Create OneDrive subcommand
        var oneDriveCommand = new Command("onedrive", "OneDrive source plugin");

        // Add init and download subcommands
        oneDriveCommand.Subcommands.Add(OneDriveInitCommand.Create());
        oneDriveCommand.Subcommands.Add(OneDriveSourceCommand.Create(_services));

        sourceCommand.Subcommands.Add(oneDriveCommand);

        yield return sourceCommand;
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
