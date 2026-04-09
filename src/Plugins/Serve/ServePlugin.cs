using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Plugins.Serve.Configuration;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Serve;

/// <summary>
/// Serve plugin for Revela - local HTTP server for previewing generated sites
/// </summary>
public sealed class ServePlugin : IPlugin
{
    /// <inheritdoc />
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Serve",
        Name = "Serve",
        Version = "1.0.0",
        Description = "Local HTTP server for previewing generated sites",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration via SDK helper
        // Binds to project.json section, validates DataAnnotations on access
        services.AddPluginConfig<ServePluginConfig>();

        // Register Commands for Dependency Injection
        services.AddTransient<ServeCommand>();
        services.AddTransient<ConfigServeCommand>();
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Resolve commands from DI container
        var serveCommand = services.GetRequiredService<ServeCommand>();
        var configCommand = services.GetRequiredService<ConfigServeCommand>();

        // 1. Register serve command at root level → revela serve
        //    Group "Build" places it with generate and clean
        //    Order 15 places it between generate (10) and clean (20)
        //    Requires project (serves project's output folder)
        yield return new CommandDescriptor(
            serveCommand.Create(),
            ParentCommand: null,
            Order: 15,
            Group: "Build");

        // 2. Register config command → revela config serve
        //    Doesn't require project (config file is independent)
        yield return new CommandDescriptor(
            configCommand.Create(),
            ParentCommand: "config",
            Group: "Addons",
            RequiresProject: false);
    }
}
