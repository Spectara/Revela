using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Services;

namespace Spectara.Revela.Commands.Generate;

/// <summary>
/// Extension methods for registering Generate feature services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Generate feature services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGenerateFeature(this IServiceCollection services)
    {
        // Bind RevelaConfig.Generate section via IConfigureOptions (resolves IConfiguration from DI)
        services.AddSingleton<IConfigureOptions<RevelaConfig>, ConfigureRevelaConfig>();

        // Services
        services.AddSingleton<CameraModelTransformer>();
        services.AddSingleton<ImageManifestService>();
        services.AddSingleton<IImageProcessor, NetVipsImageProcessor>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
        services.AddSingleton<FrontMatterParser>();
        services.AddSingleton<ContentScanner>();
        services.AddSingleton<NavigationBuilder>();
        services.AddSingleton<SiteGenerator>();

        // Commands
        services.AddTransient<GenerateCommand>();

        return services;
    }
}

/// <summary>
/// Configures RevelaConfig by binding the "generate" section from IConfiguration.
/// </summary>
/// <remarks>
/// Uses IConfigureOptions pattern to defer IConfiguration resolution until runtime.
/// This allows AddGenerateFeature() to work without an IConfiguration parameter.
/// </remarks>
internal sealed class ConfigureRevelaConfig(IConfiguration configuration) : IConfigureOptions<RevelaConfig>
{
    public void Configure(RevelaConfig options) =>
        configuration.GetSection("generate").Bind(options.Generate);
}
