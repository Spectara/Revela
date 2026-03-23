using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Core.Generate;

/// <summary>
/// Core generation plugin — scans content, renders pages, and processes images.
/// </summary>
public sealed class GeneratePlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Core.Generate",
        Name = "Generate",
        Version = "1.0.0",
        Description = "Core site generation pipeline (scan, render, images)",
        Author = "Spectara",
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) => services.AddGenerateFeature();

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Generate parent command
        yield return new CommandDescriptor(
            Commands.GenerateCommand.Create(),
            Order: 10,
            Group: "Build",
            RequiresProject: true);

        // Generate pipeline steps (IsSequentialStep → included in "all" command)
        var allCommand = services.GetRequiredService<Commands.AllCommand>();
        yield return new CommandDescriptor(
            allCommand.Create(),
            ParentCommand: "generate",
            Order: 0);

        var scanCommand = services.GetRequiredService<Commands.ScanCommand>();
        yield return new CommandDescriptor(
            scanCommand.Create(),
            ParentCommand: "generate",
            Order: 10,
            IsSequentialStep: true);

        var pagesCommand = services.GetRequiredService<Commands.PagesCommand>();
        yield return new CommandDescriptor(
            pagesCommand.Create(),
            ParentCommand: "generate",
            Order: 30,
            IsSequentialStep: true);

        var imagesCommand = services.GetRequiredService<Commands.ImagesCommand>();
        yield return new CommandDescriptor(
            imagesCommand.Create(),
            ParentCommand: "generate",
            Order: 40,
            IsSequentialStep: true);

        // Clean parent command
        yield return new CommandDescriptor(
            Commands.CleanCommand.Create(),
            Order: 20,
            Group: "Build",
            RequiresProject: true);

        // Clean pipeline steps (IsSequentialStep → included in "all" command)
        var cleanAllCommand = services.GetRequiredService<Commands.CleanAllCommand>();
        yield return new CommandDescriptor(
            cleanAllCommand.Create(),
            ParentCommand: "clean",
            Order: 0);

        var cleanOutputCommand = services.GetRequiredService<Commands.CleanOutputCommand>();
        yield return new CommandDescriptor(
            cleanOutputCommand.Create(),
            ParentCommand: "clean",
            Order: 10,
            IsSequentialStep: true);

        var cleanImagesCommand = services.GetRequiredService<Commands.CleanImagesCommand>();
        yield return new CommandDescriptor(
            cleanImagesCommand.Create(),
            ParentCommand: "clean",
            Order: 15,
            IsSequentialStep: true);

        var cleanCacheCommand = services.GetRequiredService<Commands.CleanCacheCommand>();
        yield return new CommandDescriptor(
            cleanCacheCommand.Create(),
            ParentCommand: "clean",
            Order: 20,
            IsSequentialStep: true);

        // Create command
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
            Order: 40,
            Group: "Project");

        var configSortingCommand = services.GetRequiredService<Commands.ConfigSortingCommand>();
        yield return new CommandDescriptor(
            configSortingCommand.Create(),
            ParentCommand: "config",
            Order: 50,
            Group: "Project");

        var configPathsCommand = services.GetRequiredService<Commands.ConfigPathsCommand>();
        yield return new CommandDescriptor(
            configPathsCommand.Create(),
            ParentCommand: "config",
            Order: 30,
            Group: "Project");
    }
}
