using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Reads the manifest file for plugins without touching the scan's in-memory state.
/// </summary>
internal sealed class ManifestReader(
    ILogger<ManifestReader> logger,
    IOptions<ProjectEnvironment> projectEnvironment) : IManifestReader
{
    /// <inheritdoc />
    public async Task<ManifestSnapshot?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await ManifestService.ReadAsync(
            ManifestService.GetManifestPath(projectEnvironment.Value.Path),
            logger,
            cancellationToken);
        if (manifest?.Root is not { } root)
        {
            return null;
        }

        var images = new Dictionary<string, ImageContent>(StringComparer.Ordinal);
        foreach (var (sourcePath, image, _) in ManifestService.EnumerateImages(root))
        {
            images[sourcePath] = image;
        }

        return new ManifestSnapshot { Root = root, Images = images };
    }
}
