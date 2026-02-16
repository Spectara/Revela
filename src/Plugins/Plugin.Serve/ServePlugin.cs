using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Plugin.Serve.Configuration;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Serve plugin for Revela - local HTTP server for previewing generated sites
/// </summary>
public sealed class ServePlugin : IPlugin
{
    private IServiceProvider? services;

    /// <inheritdoc />
    public IPluginMetadata Metadata { get; } = new PluginMetadata
    {
        Name = "Serve",
        Version = "1.0.0",
        Description = "Local HTTP server for previewing generated sites",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Nothing to do - plugin config is stored in project.json via IConfigService.
        // Environment variables with SPECTARA__REVELA__ prefix are auto-loaded.
        // Example ENV: SPECTARA__REVELA__PLUGIN__SERVE__PORT=3000
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration (IOptions pattern with validation)
        services.AddOptions<ServeConfig>()
            .BindConfiguration(ServeConfig.SectionName)
            .ValidateDataAnnotations();

        // Register Commands for Dependency Injection
        services.AddTransient<ServeCommand>();
        services.AddTransient<ConfigServeCommand>();
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
            RequiresProject: false);
    }
}
