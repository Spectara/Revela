using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Spectara.Revela.Plugins.Compress.Commands;
using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Compress;

/// <summary>
/// Compression plugin for Revela - compresses static files with Gzip and Brotli.
/// </summary>
/// <remarks>
/// <para>
/// Creates .gz and .br files alongside originals for improved hosting on
/// platforms without native compression (GitHub Pages, S3, etc.).
/// </para>
/// <para>
/// Supported file types: .html, .css, .js, .json, .svg, .xml
/// </para>
/// </remarks>
public sealed class CompressPlugin : IPlugin
{
    // Menu position only (compress is opt-in, not a sequential step): after images.
    private const int GenerateOrder = PipelineOrder.Images + 100;

    // Sidecars are removed after the host's cache clean and other plugin data.
    private const int CleanOrder = CleanPipelineOrder.Cache + 200;

    /// <inheritdoc />
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Compress",
        Name = "Static Compression",
        Version = PackageVersion.FromAssembly(typeof(CompressPlugin).Assembly),
        Description = "Compress static files with Gzip and Brotli",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register compression service
        services.TryAddTransient<CompressionService>();
        services.TryAddTransient<CompressedSiteInvalidator>();

        // Register commands
        services.TryAddTransient<CompressCommand>();
        services.TryAddTransient<CleanCompressCommand>();

        // Invalidate pre-compressed sidecars before the rendered site changes.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IArtifactInvalidator, CompressedSiteInvalidator>());

        // Register clean step as pipeline step for engine orchestration
        // Note: CompressCommand is NOT a pipeline step — pre-compression is opt-in
        services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, CleanCompressCommand>());
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Resolve commands from DI container
        var compressCommand = services.GetRequiredService<CompressCommand>();
        var cleanCompressCommand = services.GetRequiredService<CleanCompressCommand>();

        // Register compress command → revela generate compress
        yield return new CommandDescriptor(
            compressCommand.Create(),
            ParentCommand: "generate",
            Order: GenerateOrder);

        // Register clean compress command → revela clean compress
        yield return new CommandDescriptor(
            cleanCompressCommand.Create(),
            ParentCommand: "clean",
            Order: CleanOrder,
            IsSequentialStep: true);
    }
}
