using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Generate.Abstractions;
using Spectara.Revela.Plugins.Generate.Commands;
using Spectara.Revela.Plugins.Generate.Infrastructure;
using Spectara.Revela.Plugins.Generate.Services;
using Spectara.Revela.Plugins.Generate.Templates;
using Spectara.Revela.Plugins.Generate.Wizard;
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

        // Core services (TryAdd for idempotent registration — safe when called by both
        // AddRevelaCommands and plugin loader)
        services.TryAddSingleton<IFileHashService, FileHashService>();
        services.TryAddSingleton<IImageSizesProvider, ImageSizesProvider>();

        // Parsing, Scanning, Building, Mapping (static classes not registered: GallerySorter, UrlBuilder)
        services.TryAddSingleton<RevelaParser>();
        services.TryAddSingleton<ContentScanner>();
        services.TryAddSingleton<NavigationBuilder>();
        services.TryAddSingleton<CameraModelMapper>();

        // Infrastructure services
        services.TryAddSingleton<IMarkdownService, MarkdownService>();
        services.TryAddSingleton<IImageProcessor, NetVipsImageProcessor>();
        services.TryAddTransient<ITemplateEngine, ScribanTemplateEngine>();
        services.TryAddTransient<Func<ITemplateEngine>>(sp => () => sp.GetRequiredService<ITemplateEngine>());
        services.TryAddSingleton<ITemplateResolver, TemplateResolver>();
        services.TryAddSingleton<IAssetResolver, AssetResolver>();
        services.TryAddSingleton<IStaticFileService, StaticFileService>();
        services.TryAddSingleton<IManifestRepository, ManifestService>();

        // Domain services (three main services)
        services.TryAddSingleton<IContentService, ContentService>();
        services.TryAddSingleton<IImageService, ImageService>();
        services.TryAddTransient<IRenderService, RenderService>();

        // Engine facade (public API for MCP, GUI, and other plugins)
        services.TryAddTransient<IRevelaEngine, RevelaEngine>();

        // Commands (thin CLI wrappers)
        services.TryAddTransient<AllCommand>();
        services.TryAddTransient<ScanCommand>();
        services.TryAddTransient<ImagesCommand>();
        services.TryAddTransient<PagesCommand>();

        // Register commands as generate steps for pipeline orchestration
        // (IGenerateStep is multi-registration — TryAddEnumerable prevents duplicates)
        services.TryAddEnumerable(ServiceDescriptor.Transient<IGenerateStep, ScanCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IGenerateStep, PagesCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IGenerateStep, ImagesCommand>());

        // Clean commands
        services.TryAddTransient<CleanAllCommand>();
        services.TryAddTransient<CleanOutputCommand>();
        services.TryAddTransient<CleanImagesCommand>();
        services.TryAddTransient<CleanCacheCommand>();

        // Register clean commands as clean steps for pipeline orchestration
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICleanStep, CleanOutputCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICleanStep, CleanImagesCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICleanStep, CleanCacheCommand>());

        // Create commands + page templates
        services.TryAddTransient<CreateCommand>();
        services.TryAddTransient<CreatePageCommand>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPageTemplate, GalleryPageTemplate>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPageTemplate, TextPageTemplate>());

        // Config commands (generate-related)
        services.TryAddTransient<ConfigImageCommand>();
        services.TryAddTransient<ConfigSortingCommand>();
        services.TryAddTransient<ConfigPathsCommand>();

        // Wizard steps (project setup)
        services.TryAddEnumerable(ServiceDescriptor.Transient<IWizardStep, PathsWizardStep>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IWizardStep, ImagesWizardStep>());

        return services;
    }
}
