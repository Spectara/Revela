using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Extension methods for registering CLI hosting services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds interactive mode services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInteractiveMode(this IServiceCollection services)
    {
        services.AddSingleton<CommandGroupRegistry>();
        services.AddSingleton<CommandOrderRegistry>();
        services.AddSingleton<PipelineStepOrderProvider>();
        services.AddSingleton<IPipelineStepOrderProvider>(sp => sp.GetRequiredService<PipelineStepOrderProvider>());
        services.AddTransient<CommandExecutor>();
        services.AddTransient<IInteractiveMenuService, InteractiveMenuService>();

        // Note: Wizards are registered in Commands.ServiceCollectionExtensions.AddRevelaCommands()

        return services;
    }
}
