using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
        Version = PackageVersion.FromAssembly(typeof(ServePlugin).Assembly),
        Description = "Local HTTP server for previewing generated sites",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // BindConfiguration must live in user-written source so the .NET
        // Configuration Binding Source Generator can intercept it for trim/AOT.
        // The Section constant is hand-written on the config class; the SDK generator
        // reports REVELA002 if it differs from the [RevelaConfig] attribute.
        services.AddOptions<ServePluginConfig>()
            .BindConfiguration(ServePluginConfig.Section);

        // Trim/AOT-safe DataAnnotations validation via the
        // [OptionsValidator] source generator. TryAddEnumerable keeps it idempotent.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ServePluginConfig>, ServePluginConfigValidator>());

        // Register Commands for Dependency Injection (idempotent)
        services.TryAddTransient<ServeCommand>();
        services.TryAddTransient<ConfigServeCommand>();
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
        //    Writes project.json, so it requires a project (like every plugin config command)
        yield return new CommandDescriptor(
            configCommand.Create(),
            ParentCommand: "config",
            Group: "Addons");
    }
}
