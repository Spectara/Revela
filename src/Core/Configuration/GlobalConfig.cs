namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Global CLI configuration stored in revela.json
/// </summary>
/// <remarks>
/// <para>
/// This configuration is stored next to revela.exe (portable) or in %APPDATA%/Revela (dotnet tool).
/// It contains settings that apply to the CLI itself, not to individual projects.
/// </para>
/// <para>
/// Location is determined by <see cref="Services.ConfigPathResolver"/>.
/// </para>
/// </remarks>
public sealed class GlobalConfig
{
    /// <summary>
    /// Additional NuGet feeds for plugin installation
    /// </summary>
    /// <remarks>
    /// nuget.org is always available as built-in source.
    /// </remarks>
#pragma warning disable CA1002 // Do not expose generic lists - needed for JSON serialization
    public List<FeedConfig> Feeds { get; init; } = [];
#pragma warning restore CA1002

    /// <summary>
    /// Logging configuration
    /// </summary>
    public GlobalLoggingConfig Logging { get; init; } = new();

    /// <summary>
    /// Default settings for new projects
    /// </summary>
    public DefaultsConfig Defaults { get; init; } = new();

    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckUpdates { get; init; } = true;
}

/// <summary>
/// NuGet feed configuration
/// </summary>
public sealed class FeedConfig
{
    /// <summary>
    /// Display name for the feed
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// NuGet v3 API URL or local directory path
    /// </summary>
    /// <remarks>
    /// Can be a URL (https://...) or a local file path (./plugins, C:\feeds).
    /// String type used for JSON serialization and to support both URLs and paths.
    /// </remarks>
#pragma warning disable CA1056 // URI properties should not be strings - supports both URLs and local paths
    public required string Url { get; init; }
#pragma warning restore CA1056
}

/// <summary>
/// Logging configuration for global config
/// </summary>
public sealed class GlobalLoggingConfig
{
    /// <summary>
    /// Log levels per category
    /// </summary>
    /// <remarks>
    /// Keys are category names (namespace prefixes), values are log level names.
    /// "Default" is the fallback for categories not explicitly configured.
    /// Same structure as standard .NET logging configuration.
    /// </remarks>
    public Dictionary<string, string> LogLevel { get; init; } = new()
    {
        ["Default"] = "Warning",
        ["Spectara.Revela"] = "Warning",
        ["Microsoft"] = "Warning",
        ["System"] = "Warning"
    };
}

/// <summary>
/// Default settings for new projects
/// </summary>
public sealed class DefaultsConfig
{
    /// <summary>
    /// Default theme for new projects
    /// </summary>
    public string Theme { get; init; } = "Lumina";
}
