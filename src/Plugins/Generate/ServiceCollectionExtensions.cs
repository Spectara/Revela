using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Generate.Abstractions;
using Spectara.Revela.Plugins.Generate.Commands;
using Spectara.Revela.Plugins.Generate.Infrastructure;
using Spectara.Revela.Plugins.Generate.Services;
using Spectara.Revela.Plugins.Generate.Templates;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Abstractions.Engine;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Plugins.Generate;

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
        services.AddSingleton<IImageSizesProvider, ImageSizesProvider>();

        // Parsing, Scanning, Building, Mapping (static classes not registered: GallerySorter, UrlBuilder)
        services.AddSingleton<RevelaParser>();
        services.AddSingleton<ContentScanner>();
        services.AddSingleton<NavigationBuilder>();
        services.AddSingleton<CameraModelMapper>();

        // Infrastructure services
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddSingleton<IImageProcessor, NetVipsImageProcessor>();
        services.AddTransient<ITemplateEngine, ScribanTemplateEngine>();
        services.AddTransient<Func<ITemplateEngine>>(sp => () => sp.GetRequiredService<ITemplateEngine>());
        services.AddSingleton<ITemplateResolver, TemplateResolver>();
        services.AddSingleton<IAssetResolver, AssetResolver>();
        services.AddSingleton<IStaticFileService, StaticFileService>();
        services.AddSingleton<IManifestRepository, ManifestService>();

        // Domain services (three main services)
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IImageService, ImageService>();
        services.AddTransient<IRenderService, RenderService>();

        // Engine facade (public API for MCP, GUI, and other plugins)
        services.AddTransient<IRevelaEngine, RevelaEngine>();

        // Commands (thin CLI wrappers) - also registered as IGenerateStep
        services.AddTransient<AllCommand>();
        services.AddTransient<ScanCommand>();
        services.AddTransient<ImagesCommand>();
        services.AddTransient<PagesCommand>();
        services.AddTransient<GenerateCommand>();

        // Register commands as generate steps for pipeline orchestration
        services.AddTransient<IGenerateStep, ScanCommand>();
        services.AddTransient<IGenerateStep, PagesCommand>();
        services.AddTransient<IGenerateStep, ImagesCommand>();

        // Clean commands
        services.AddTransient<CleanCommand>();
        services.AddTransient<CleanAllCommand>();
        services.AddTransient<CleanOutputCommand>();
        services.AddTransient<CleanImagesCommand>();
        services.AddTransient<CleanCacheCommand>();

        // Create commands + page templates
        services.AddTransient<CreateCommand>();
        services.AddTransient<CreatePageCommand>();
        services.AddSingleton<IPageTemplate, GalleryPageTemplate>();
        services.AddSingleton<IPageTemplate, TextPageTemplate>();

        // Config commands (generate-related)
        services.AddTransient<ConfigImageCommand>();
        services.AddTransient<ConfigSortingCommand>();
        services.AddTransient<ConfigPathsCommand>();

        return services;
    }
}
