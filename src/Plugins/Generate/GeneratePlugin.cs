using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Generate;

/// <summary>
/// Core generation plugin — scans content, renders pages, and processes images.
/// </summary>
public sealed class GeneratePlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new()
    {
        Name = "Generate",
        Version = "1.0.0",
        Description = "Core site generation pipeline (scan, render, images)",
        Author = "Spectara",
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Services will be registered here as code is migrated from Commands
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var generateCommand = services.GetRequiredService<Commands.GenerateCommand>();
        yield return new CommandDescriptor(
            generateCommand.Create(),
            Order: 10,
            Group: "Build",
            RequiresProject: true);

        var cleanCommand = services.GetRequiredService<Commands.CleanCommand>();
        yield return new CommandDescriptor(
            cleanCommand.Create(),
            Order: 20,
            Group: "Build",
            RequiresProject: true);

        var createCommand = services.GetRequiredService<Commands.CreateCommand>();
        yield return new CommandDescriptor(
            createCommand.Create(),
            Order: 10,
            Group: "Content",
            RequiresProject: true);

        // Config subcommands registered under "config" parent
        var configImageCommand = services.GetRequiredService<Commands.ConfigImageCommand>();
        yield return new CommandDescriptor(
            configImageCommand.Create(),
            ParentCommand: "config",
            Order: 40);

        var configSortingCommand = services.GetRequiredService<Commands.ConfigSortingCommand>();
        yield return new CommandDescriptor(
            configSortingCommand.Create(),
            ParentCommand: "config",
            Order: 50);

        var configPathsCommand = services.GetRequiredService<Commands.ConfigPathsCommand>();
        yield return new CommandDescriptor(
            configPathsCommand.Create(),
            ParentCommand: "config",
            Order: 30);
    }
}
