using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Features.Generate.Services;

internal static class PhotoViewerResolver
{
    public static PhotoViewerMode Resolve(
        string? pageValue,
        PhotoViewerMode? projectValue,
        ThemeManifest manifest,
        string sourcePath,
        string themeName)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(themeName);

        var capabilities = manifest.PhotoViewer ?? throw new ArgumentException(
            $"Theme '{themeName}' does not declare photo viewer capabilities.",
            nameof(manifest));
        var supportedValues = FormatSupported(capabilities.Supported);

        if (!string.IsNullOrWhiteSpace(pageValue))
        {
            var mode = ParsePageValue(pageValue, sourcePath, themeName, supportedValues);
            if (!capabilities.Supported.Contains(mode))
            {
                throw new PhotoViewerResolutionException(
                    $"Unsupported photo_viewer value '{mode.ToValue()}' in '{sourcePath}' for theme '{themeName}'. " +
                    $"Supported modes: {supportedValues}.");
            }

            return mode;
        }

        if (projectValue is { } projectMode)
        {
            if (!capabilities.Supported.Contains(projectMode))
            {
                throw new PhotoViewerResolutionException(
                    $"Unsupported project.json key theme.photoViewer value '{projectMode.ToValue()}' for theme '{themeName}'. " +
                    $"Supported modes: {supportedValues}.");
            }

            return projectMode;
        }

        if (!capabilities.Supported.Contains(capabilities.Default))
        {
            throw new PhotoViewerResolutionException(
                $"Theme '{themeName}' default photo viewer value '{capabilities.Default.ToValue()}' is unsupported. " +
                $"Supported modes: {supportedValues}.");
        }

        return capabilities.Default;
    }

    private static PhotoViewerMode ParsePageValue(
        string pageValue,
        string sourcePath,
        string themeName,
        string supportedValues)
    {
        if (PhotoViewerModeValues.TryParse(pageValue, out var mode))
        {
            return mode;
        }

        throw new PhotoViewerResolutionException(
            $"Unknown photo_viewer value '{pageValue}' in '{sourcePath}' for theme '{themeName}'. " +
            $"Supported modes: {supportedValues}.");
    }

    private static string FormatSupported(IReadOnlyList<PhotoViewerMode> supported) =>
        string.Join(", ", supported.Select(PhotoViewerModeValues.ToValue));

}

internal sealed class PhotoViewerResolutionException : InvalidOperationException
{
    public PhotoViewerResolutionException()
    {
    }

    public PhotoViewerResolutionException(string message)
        : base(message)
    {
    }

    public PhotoViewerResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
