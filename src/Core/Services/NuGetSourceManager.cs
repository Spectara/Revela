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
/// </remarks>
public static class NuGetSourceManager
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

    /// <summary>
    /// Loads all configured sources (including built-in nuget.org)
    /// </summary>
    public static async Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = new List<NuGetSource> { DefaultSource };

        var config = await GlobalConfigManager.LoadAsync(cancellationToken);
        foreach (var feed in config.Feeds)
        {
            sources.Add(new NuGetSource
            {
                Name = feed.Name,
                Url = feed.Url,
                Enabled = true
            });
        }

        return sources;
    }

    /// <summary>
    /// Gets all sources with location info for display
    /// </summary>
    public static async Task<List<(NuGetSource Source, string Location)>> GetAllSourcesWithLocationAsync(CancellationToken cancellationToken = default)
    {
        var sources = new List<(NuGetSource Source, string Location)>
        {
            (DefaultSource, "built-in")
        };

        var config = await GlobalConfigManager.LoadAsync(cancellationToken);
        foreach (var feed in config.Feeds)
        {
            var location = feed.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? "remote"
                : "local";

            sources.Add((new NuGetSource
            {
                Name = feed.Name,
                Url = feed.Url,
                Enabled = true
            }, location));
        }

        return sources;
    }

    /// <summary>
    /// Gets all sources including built-in
    /// </summary>
    public static async Task<List<NuGetSource>> GetAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sourcesWithLocation = await GetAllSourcesWithLocationAsync(cancellationToken);
        return [.. sourcesWithLocation.Select(s => s.Source)];
    }

    /// <summary>
    /// Adds a new NuGet source
    /// </summary>
    /// <remarks>
    /// Delegates to GlobalConfigManager for consistent config management.
    /// </remarks>
#pragma warning disable CA1054 // URI parameters should not be strings - string required for user input
    public static Task AddSourceAsync(string name, string url, CancellationToken cancellationToken = default)
#pragma warning restore CA1054
    {
        return GlobalConfigManager.AddFeedAsync(name, url, cancellationToken);
    }

    /// <summary>
    /// Removes a NuGet source
    /// </summary>
    /// <remarks>
    /// Delegates to GlobalConfigManager for consistent config management.
    /// </remarks>
    public static Task<bool> RemoveSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in source 'nuget.org'");
        }

        return GlobalConfigManager.RemoveFeedAsync(name, cancellationToken);
    }
}
