using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Features.Generate.Templates;
using Spectara.Revela.Features.Generate.Wizard;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Abstractions.Engine;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Features.Generate;

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
        services.TryAddSingleton<IManifestReader, ManifestReader>();
        services.TryAddSingleton<ImageStateStore>();

        // Revela core owns its artifacts like any package: one invalidator each, with its kind.
        services.TryAddEnumerable(ServiceDescriptor.Transient<IArtifactInvalidator, ManifestInvalidator>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IArtifactInvalidator, RenderedSiteInvalidator>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IArtifactInvalidator, ProcessedImagesInvalidator>());

        // Domain services (three main services)
        services.TryAddSingleton<IContentService, ContentService>();
        services.TryAddSingleton<IImageService, ImageService>();
        services.TryAddTransient<IRenderService, RenderService>();

        // Structural checks (host units) — order here is the collect-all report order.
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, ConfigCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, StructureCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, ThemeCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, ContentCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, SlugsCheck>());

        // Aggregator behind the `check` report.
        services.TryAddSingleton<ISiteValidator, CheckService>();

        // Engine facade (public API for MCP, GUI, and other plugins)
        services.TryAddTransient<IRevelaEngine, RevelaEngine>();

        // Commands (thin CLI wrappers + IPipelineStep implementations)
        services.TryAddTransient<CheckCommand>();
        services.TryAddTransient<ScanCommand>();
        services.TryAddTransient<ImagesCommand>();
        services.TryAddTransient<PagesCommand>();

        // Register commands as pipeline steps for engine orchestration
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, ScanCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, PagesCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, ImagesCommand>());

        // Clean commands
        services.TryAddTransient<KindClean>();
        services.TryAddTransient<CleanAllCommand>();
        services.TryAddTransient<CleanOutputCommand>();
        services.TryAddTransient<CleanImagesCommand>();
        services.TryAddTransient<CleanCacheCommand>();

        // Register clean commands as pipeline steps
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CleanOutputCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CleanImagesCommand>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CleanCacheCommand>());

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

