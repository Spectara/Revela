using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Features.Packages.Services;

namespace Spectara.Revela.Features.Packages.Logging;

/// <summary>
/// High-performance logging for PackageManager using source-generated extension methods.
/// </summary>
internal static partial class PackageManagerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin: {PackageId}")]
    public static partial void InstallingPlugin(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Package {PackageId} not found")]
    public static partial void PackageNotFound(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {PackageId} v{Version}")]
    public static partial void InstallingVersion(this ILogger<PackageManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin {PackageId}")]
    public static partial void InstallFailed(this ILogger<PackageManager> logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin: {PackageId}")]
    public static partial void UninstallingPlugin(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalled {PackageId}")]
    public static partial void PluginUninstalled(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PackageId} not found")]
    public static partial void PluginNotFound(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin {PackageId}")]
    public static partial void UninstallFailed(this ILogger<PackageManager> logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from file: {FilePath}")]
    public static partial void InstallingFromFile(this ILogger<PackageManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Local plugin package file was not found: {FilePath}")]
    public static partial void LocalPackageNotFound(this ILogger<PackageManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Local plugin package must be a .nupkg file: {FilePath}")]
    public static partial void LocalPackageNotNupkg(this ILogger<PackageManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from URL: {Url}")]
    private static partial void LogInstallingFromUrl(ILogger<PackageManager> logger, string url);

    /// <summary>Logs a package URL install; the URL is redacted (no user info, query or fragment).</summary>
    public static void InstallingFromUrl(this ILogger<PackageManager> logger, string url)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var redacted = UrlRedaction.Redact(url);
            LogInstallingFromUrl(logger, redacted);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download package {PackageId} v{Version}")]
    public static partial void DownloadFailed(this ILogger<PackageManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PackageId} installed successfully")]
    public static partial void PluginInstalled(this ILogger<PackageManager> logger, string packageId);

    // Multi-source discovery logging. Every source/URL argument is passed through
    // UrlRedaction because feed URLs may carry credentials or signed query strings.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Using named source '{SourceName}' -> {Url}")]
    private static partial void LogUsingNamedSource(ILogger<PackageManager> logger, string sourceName, string url);

    public static void UsingNamedSource(this ILogger<PackageManager> logger, string sourceName, string url)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var redacted = UrlRedaction.Redact(url);
            LogUsingNamedSource(logger, sourceName, redacted);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Source '{Source}' not found in config, treating as path/URL")]
    private static partial void LogSourceNotFoundTreatingAsUrl(ILogger<PackageManager> logger, string source);

    public static void SourceNotFoundTreatingAsUrl(this ILogger<PackageManager> logger, string source)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var redacted = UrlRedaction.Redact(source);
            LogSourceNotFoundTreatingAsUrl(logger, redacted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying {SourceCount} source(s) for package {PackageId}")]
    public static partial void TryingMultipleSources(this ILogger<PackageManager> logger, string packageId, int sourceCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trying source '{SourceName}' ({Url})")]
    private static partial void LogTryingSource(ILogger<PackageManager> logger, string sourceName, string url);

    public static void TryingSource(this ILogger<PackageManager> logger, string sourceName, string url)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var redacted = UrlRedaction.Redact(url);
            LogTryingSource(logger, sourceName, redacted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully installed {PackageId} from source '{SourceName}'")]
    public static partial void SuccessFromSource(this ILogger<PackageManager> logger, string packageId, string sourceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Source '{SourceName}' failed, trying next")]
    public static partial void SourceFailed(this ILogger<PackageManager> logger, string sourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "All sources failed for package {PackageId}")]
    public static partial void AllSourcesFailed(this ILogger<PackageManager> logger, string packageId);

    // Package trust
    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid package ID '{PackageId}': expected a NuGet package ID (letters, digits, '_', separated by single '.' or '-')")]
    public static partial void InvalidPackageId(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid version '{Version}' for package {PackageId}: expected an exact NuGet version (e.g. 1.2.0) or 'latest'")]
    public static partial void InvalidVersion(this ILogger<PackageManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rejected insecure package source {Source}: use https:// or a local folder (plain http:// is only allowed for localhost)")]
    private static partial void LogInsecureSourceRejected(ILogger<PackageManager> logger, string source);

    public static void InsecureSourceRejected(this ILogger<PackageManager> logger, string source)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            var redacted = UrlRedaction.Redact(source);
            LogInsecureSourceRejected(logger, redacted);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping insecure package source '{SourceName}' ({Url}): use https:// or a local folder")]
    private static partial void LogInsecureSourceSkipped(ILogger<PackageManager> logger, string sourceName, string url);

    public static void InsecureSourceSkipped(this ILogger<PackageManager> logger, string sourceName, string url)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var redacted = UrlRedaction.Redact(url);
            LogInsecureSourceSkipped(logger, sourceName, redacted);
        }
    }
}


