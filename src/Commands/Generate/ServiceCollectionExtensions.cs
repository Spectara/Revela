using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Commands;
using Spectara.Revela.Commands.Generate.Mapping;
using Spectara.Revela.Commands.Generate.Parsing;
using Spectara.Revela.Commands.Generate.Pipeline;
using Spectara.Revela.Commands.Generate.Scanning;
using Spectara.Revela.Commands.Generate.Services;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

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
        // GenerateConfig is bound via AddRevelaConfigSections() in Program.cs

        // Core services
        services.AddSingleton<IFileHashService, FileHashService>();

        // Parsing, Scanning, Building, Mapping (static classes not registered: GallerySorter, UrlBuilder)
        services.AddSingleton<RevelaParser>();
        services.AddSingleton<ContentScanner>();
        services.AddSingleton<NavigationBuilder>();
        services.AddSingleton<CameraModelMapper>();

        // Infrastructure services
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddSingleton<IImageProcessor, NetVipsImageProcessor>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
        services.AddSingleton<ITemplateResolver, TemplateResolver>();
        services.AddSingleton<IAssetResolver, AssetResolver>();
        services.AddSingleton<IManifestRepository, ManifestService>();

        // Domain services (three main services)
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<IRenderService, RenderService>();

        // Commands (thin CLI wrappers)
        services.AddTransient<AllCommand>();
        services.AddTransient<ScanCommand>();
        services.AddTransient<ImagesCommand>();
        services.AddTransient<PagesCommand>();
        services.AddTransient<GenerateCommand>();

        // Pipeline steps (auto-discovered by AllCommand via DI)
        services.AddTransient<IGeneratePipelineStep, ScanPipelineStep>();
        services.AddTransient<IGeneratePipelineStep, PagesPipelineStep>();
        services.AddTransient<IGeneratePipelineStep, ImagesPipelineStep>();

        return services;
    }
}
