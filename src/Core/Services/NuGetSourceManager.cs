using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Manages NuGet package sources configuration
/// </summary>
/// <remarks>
/// <para>
/// Feeds are stored in the unified revela.json config file.
/// Use <c>revela config feed add/remove/list</c> to manage feeds.
/// </para>
/// <para>
/// Built-in source (nuget.org) is always available.
/// </para>
/// <para>
/// Relative paths in feed URLs are resolved relative to the config file location,
/// making configurations portable across different machines.
/// </para>
/// </remarks>
public sealed partial class NuGetSourceManager(
    ILogger<NuGetSourceManager> logger,
    IOptionsMonitor<FeedsConfig> feedsConfig) : INuGetSourceManager
{
    /// <summary>
    /// Gets the path to the config file
    /// </summary>
    /// <remarks>
    /// Delegates to GlobalConfigManager for consistent config location.
    /// </remarks>
    public static string ConfigFilePath => GlobalConfigManager.ConfigFilePath;

    /// <summary>
    /// Gets the default NuGet.org source
    /// </summary>
    public static NuGetSource DefaultSource => new()
    {
        Name = "nuget.org",
        Url = "https://api.nuget.org/v3/index.json",
        Enabled = true
    };

    /// <inheritdoc/>
    public Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = new List<NuGetSource> { DefaultSource };

        var config = feedsConfig.CurrentValue;
        foreach (var (name, url) in config.Feeds)
        {
            var resolvedUrl = ResolvePathIfRelative(url);
            sources.Add(new NuGetSource
            {
                Name = name,
                Url = resolvedUrl,
                Enabled = true
            });
        }

        return Task.FromResult(sources);
    }

    /// <inheritdoc/>
    public Task<List<(NuGetSource Source, string Location)>> GetAllSourcesWithLocationAsync(CancellationToken cancellationToken = default)
    {
        var sources = new List<(NuGetSource Source, string Location)>
        {
            (DefaultSource, "built-in")
        };

        var config = feedsConfig.CurrentValue;
        foreach (var (name, url) in config.Feeds)
        {
            var resolvedUrl = ResolvePathIfRelative(url);
            var location = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? "remote"
                : "local";

            sources.Add((new NuGetSource
            {
                Name = name,
                Url = resolvedUrl,
                Enabled = true
            }, location));
        }

        return Task.FromResult(sources);
    }

    /// <inheritdoc/>
    public async Task<List<NuGetSource>> GetAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sourcesWithLocation = await GetAllSourcesWithLocationAsync(cancellationToken);
        return [.. sourcesWithLocation.Select(s => s.Source)];
    }

    /// <inheritdoc/>
#pragma warning disable CA1054 // URI parameters should not be strings - string required for user input
    public Task AddSourceAsync(string name, string url, CancellationToken cancellationToken = default) => GlobalConfigManager.AddFeedAsync(name, url, cancellationToken);
#pragma warning restore CA1054

    /// <inheritdoc/>
    public Task<bool> RemoveSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in source 'nuget.org'");
        }

        return GlobalConfigManager.RemoveFeedAsync(name, cancellationToken);
    }

    /// <summary>
    /// Resolves a path if it's relative, keeping HTTP URLs and absolute paths unchanged
    /// </summary>
    /// <param name="url">The URL or path to resolve</param>
    /// <returns>The resolved absolute path, or the original URL if it's HTTP or already absolute</returns>
    private string ResolvePathIfRelative(string url)
    {
        // HTTP/HTTPS URLs are never resolved
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // Absolute paths remain unchanged
        if (Path.IsPathRooted(url))
        {
            return url;
        }

        // Relative paths are resolved relative to config file location
        var configDir = Path.GetDirectoryName(ConfigFilePath);
        if (string.IsNullOrEmpty(configDir))
        {
            LogCannotResolveRelativePath(url);
            return url;
        }

        var resolvedPath = Path.GetFullPath(Path.Combine(configDir, url));
        LogResolvedRelativePath(url, resolvedPath);
        return resolvedPath;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved relative path '{RelativePath}' to '{AbsolutePath}'")]
    private partial void LogResolvedRelativePath(string relativePath, string absolutePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot resolve relative path '{RelativePath}' - config directory unknown")]
    private partial void LogCannotResolveRelativePath(string relativePath);
}
