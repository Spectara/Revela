using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;
namespace Spectara.Revela.Core.Services;

/// <summary>
/// Manages NuGet package sources configuration
/// </summary>
/// <remarks>
/// <para>
/// Feeds are declared in <c>dependencies.feeds</c> of revela.json (global) or project.json
/// (local). Use <c>revela config feed add/remove/list</c> to manage global feeds.
/// </para>
/// <para>
/// Built-in sources (bundled <c>packages/</c> folder, nuget.org) are always available.
/// </para>
/// <para>
/// Relative folder paths are resolved relative to the file that declares them.
/// </para>
/// <para>
/// Feeds declared in project.json but not in revela.json come from a (possibly foreign)
/// project and are only returned as usable sources after <see cref="ApproveProjectFeeds"/>.
/// Provenance is determined by reading both files separately because the merged
/// configuration no longer knows which file declared a key.
/// </para>
/// </remarks>
public sealed partial class NuGetSourceManager(
    ILogger<NuGetSourceManager> logger,
    IOptionsMonitor<DependenciesConfig> dependenciesConfig,
    IOptions<ProjectEnvironment> projectEnvironment,
    IGlobalConfigManager globalConfigManager) : INuGetSourceManager
{
    private const string FeedsSection = DependenciesConfig.Section + ":feeds";
    private const string ProjectFileName = "project.json";

    private volatile bool projectFeedsApproved;

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
    public string? ProjectConfigPath =>
        string.IsNullOrEmpty(projectEnvironment.Value.Path)
            ? null
            : Path.Combine(Path.GetFullPath(projectEnvironment.Value.Path), ProjectFileName);

    /// <inheritdoc/>
    public Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildSourceList().Where(IsUsable).Select(s => s.Source).ToList());

    /// <inheritdoc/>
    public Task<List<(NuGetSource Source, string Location)>> GetAllSourcesWithLocationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildSourceList().Where(IsUsable).ToList());

    /// <inheritdoc/>
    public Task<List<NuGetSource>> GetAllSourcesAsync(CancellationToken cancellationToken = default) =>
        LoadSourcesAsync(cancellationToken);

    /// <inheritdoc/>
    public IReadOnlyList<NuGetSource> GetProjectFeeds() =>
        [.. BuildSourceList().Select(s => s.Source).Where(s => s.IsProjectFeed)];

    /// <inheritdoc/>
    public IReadOnlyList<NuGetSource> GetPendingProjectFeeds() =>
        projectFeedsApproved ? [] : GetProjectFeeds();

    /// <inheritdoc/>
    public void ApproveProjectFeeds() => projectFeedsApproved = true;

    private bool IsUsable((NuGetSource Source, string Location) source) =>
        !source.Source.IsProjectFeed || projectFeedsApproved;

    /// <summary>
    /// Builds the unified source list from bundled packages, built-in defaults, and user configuration.
    /// </summary>
    private List<(NuGetSource Source, string Location)> BuildSourceList()
    {
        List<(NuGetSource Source, string Location)> sources = [];

        // Bundled packages directory (offline-first, highest priority)
        var bundledDir = ConfigPathResolver.BundledPackagesDirectory;
        if (Directory.Exists(bundledDir))
        {
            LogUsingBundledPackages(bundledDir);
            sources.Add((new NuGetSource
            {
                Name = "bundled",
                Url = bundledDir,
                Enabled = true
            }, "bundled"));
        }

        // Built-in nuget.org
        sources.Add((DefaultSource, "built-in"));

        var globalDirectory = Path.GetDirectoryName(globalConfigManager.ConfigFilePath) ?? string.Empty;
        var projectPath = ProjectConfigPath;
        var projectDirectory = projectPath is null ? null : Path.GetDirectoryName(projectPath);
        var globalFeeds = ReadFeeds(globalConfigManager.ConfigFilePath) ?? [];
        var projectFeeds = projectPath is null ? [] : ReadFeeds(projectPath);

        // Merged feeds (revela.json → project.json → environment); hot-reload via IOptionsMonitor
        foreach (var (name, url) in dependenciesConfig.CurrentValue.Feeds)
        {
            // Fail safe: when project.json cannot be read, every feed not declared globally counts as a project feed.
            var declaredByProject = projectDirectory is not null && (projectFeeds is null
                ? !globalFeeds.ContainsKey(name)
                : projectFeeds.TryGetValue(name, out var projectUrl) && string.Equals(projectUrl, url, StringComparison.Ordinal));

            var resolvedUrl = declaredByProject
                ? ResolveFeedLocation(url, projectDirectory!)
                : ResolveFeedLocation(url, globalDirectory);

            var isProjectFeed = declaredByProject
                && !(globalFeeds.TryGetValue(name, out var globalUrl)
                    && string.Equals(ResolveFeedLocation(globalUrl, globalDirectory), resolvedUrl, StringComparison.OrdinalIgnoreCase));

            if (isProjectFeed)
            {
                LogProjectFeedFound(name, resolvedUrl);
            }

            var location = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? "remote"
                : "local";

            sources.Add((new NuGetSource
            {
                Name = name,
                Url = resolvedUrl,
                Enabled = true,
                IsProjectFeed = isProjectFeed
            }, location));
        }

        return sources;
    }

    /// <summary>
    /// Reads <c>dependencies:feeds</c> from a single configuration file (no merging).
    /// </summary>
    /// <returns>The declared feeds, or <see langword="null"/> when the file exists but cannot be read.</returns>
    private Dictionary<string, string>? ReadFeeds(string filePath)
    {
        var feeds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return feeds;
        }

        try
        {
            var directory = Path.GetDirectoryName(filePath)!;
            using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile(Path.GetFileName(filePath), optional: true, reloadOnChange: false)
                .Build();

            foreach (var feed in configuration.GetSection(FeedsSection).GetChildren())
            {
                if (feed.Value is not null)
                {
                    feeds[feed.Key] = feed.Value;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            LogFeedFileUnreadable(filePath, ex.Message);
            return null;
        }

        return feeds;
    }

    /// <inheritdoc/>
    public Task AddSourceAsync(string name, string url, CancellationToken cancellationToken = default) => globalConfigManager.AddFeedAsync(name, url, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> RemoveSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in source 'nuget.org'");
        }

        return globalConfigManager.RemoveFeedAsync(name, cancellationToken);
    }

    /// <summary>
    /// Resolves a feed location, keeping HTTP URLs and absolute paths unchanged.
    /// </summary>
    /// <param name="url">The URL or path to resolve.</param>
    /// <param name="baseDirectory">Directory of the file that declares the feed.</param>
    /// <returns>The resolved absolute path, or the original URL if it's HTTP or already absolute.</returns>
    internal static string ResolveFeedLocation(string url, string baseDirectory)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        if (Path.IsPathRooted(url) || string.IsNullOrEmpty(baseDirectory))
        {
            return url;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, url));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Feed '{Name}' ({Location}) is declared only in project.json and requires consent")]
    private partial void LogProjectFeedFound(string name, string location);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read feeds from '{FilePath}' ({Error})")]
    private partial void LogFeedFileUnreadable(string filePath, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using bundled packages from '{BundledDirectory}'")]
    private partial void LogUsingBundledPackages(string bundledDirectory);
}
