using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugin.Compress.Commands;
using Spectara.Revela.Plugin.Compress.Services;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugin.Compress;

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
    private IServiceProvider? services;

    /// <inheritdoc />
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "Static Compression",
        Version = "1.0.0",
        Description = "Compress static files with Gzip and Brotli",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // No configuration needed - plugin uses sensible defaults.
        // If loaded, compression is enabled. If not loaded, no compression.
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register compression service
        services.AddTransient<CompressionService>();

        // Register commands
        services.AddTransient<CompressCommand>();
        services.AddTransient<CleanCompressCommand>();

        // Note: CompressCommand is NOT registered as IGenerateStep
        // Pre-compression requires server configuration (nginx gzip_static, etc.)
        // Users who need it can run 'revela generate compress' explicitly
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
        var compressCommand = services.GetRequiredService<CompressCommand>();
        var cleanCompressCommand = services.GetRequiredService<CleanCompressCommand>();

        // Register compress command → revela generate compress
        // Order 50 places it after images (40) in interactive menu
        yield return new CommandDescriptor(
            compressCommand.Create(),
            ParentCommand: "generate",
            Order: 50);

        // Register clean compress command → revela clean compress
        // Order 40 places it after statistics (30) in interactive menu
        yield return new CommandDescriptor(
            cleanCompressCommand.Create(),
            ParentCommand: "clean",
            Order: CleanCompressCommand.Order);
    }
}
