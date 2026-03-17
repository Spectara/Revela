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
        // Commands will be yielded here as code is migrated from Commands
        yield break;
    }
}
