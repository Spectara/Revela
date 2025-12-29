using Microsoft.Extensions.DependencyInjection;

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
        services.AddTransient<CommandPromptBuilder>();
        services.AddTransient<IInteractiveMenuService, InteractiveMenuService>();

        // Note: Wizards are registered in Commands.ServiceCollectionExtensions.AddRevelaCommands()

        return services;
    }
}
