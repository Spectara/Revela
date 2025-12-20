namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PluginManager using source-generated extension methods
/// </summary>
internal static partial class PluginManagerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin: {packageId}")]
    public static partial void InstallingPlugin(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Package {packageId} not found")]
    public static partial void PackageNotFound(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {packageId} v{version}")]
    public static partial void InstallingVersion(this ILogger<PluginManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin {packageId}")]
    public static partial void InstallFailed(this ILogger<PluginManager> logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating plugin: {packageId}")]
    public static partial void UpdatingPlugin(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin: {packageId}")]
    public static partial void UninstallingPlugin(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalled {packageId}")]
    public static partial void PluginUninstalled(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {packageId} not found")]
    public static partial void PluginNotFound(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin {packageId}")]
    public static partial void UninstallFailed(this ILogger<PluginManager> logger, Exception exception, string packageId);

    // NuGet package installation logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from file: {filePath}")]
    public static partial void InstallingFromFile(this ILogger<PluginManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from URL: {url}")]
    public static partial void InstallingFromUrl(this ILogger<PluginManager> logger, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download package {packageId} v{version}")]
    public static partial void DownloadFailed(this ILogger<PluginManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting package: {packageId} v{version}")]
    public static partial void ExtractingPackage(this ILogger<PluginManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No compatible libraries found in package {packageId}")]
    public static partial void NoCompatibleLibs(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Extracted {fileName} to {targetDir}")]
    public static partial void ExtractedFile(this ILogger<PluginManager> logger, string fileName, string targetDir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No files extracted from package {packageId}")]
    public static partial void NoFilesExtracted(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {packageId} installed successfully ({fileCount} file(s))")]
    public static partial void PluginInstalled(this ILogger<PluginManager> logger, string packageId, int fileCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created metadata file: {metadataPath}")]
    public static partial void MetadataCreated(this ILogger<PluginManager> logger, string metadataPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updated {projectJsonPath} with plugin {packageId} v{version}")]
    public static partial void ProjectJsonUpdated(this ILogger<PluginManager> logger, string projectJsonPath, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update project.json at {projectJsonPath}")]
    public static partial void ProjectJsonUpdateFailed(this ILogger<PluginManager> logger, Exception exception, string projectJsonPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed {packageId} from {projectJsonPath}")]
    public static partial void ProjectJsonPluginRemoved(this ILogger<PluginManager> logger, string projectJsonPath, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove {packageId} from {projectJsonPath}")]
    public static partial void ProjectJsonRemoveFailed(this ILogger<PluginManager> logger, Exception exception, string projectJsonPath, string packageId);

    // Multi-source discovery logging
    [LoggerMessage(Level = LogLevel.Debug, Message = "Using named source '{sourceName}' -> {url}")]
    public static partial void UsingNamedSource(this ILogger<PluginManager> logger, string sourceName, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Source '{source}' not found in config, treating as path/URL")]
    public static partial void SourceNotFoundTreatingAsUrl(this ILogger<PluginManager> logger, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying {sourceCount} source(s) for package {packageId}")]
    public static partial void TryingMultipleSources(this ILogger<PluginManager> logger, string packageId, int sourceCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trying source '{sourceName}' ({url})")]
    public static partial void TryingSource(this ILogger<PluginManager> logger, string sourceName, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully installed {packageId} from source '{sourceName}'")]
    public static partial void SuccessFromSource(this ILogger<PluginManager> logger, string packageId, string sourceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Source '{sourceName}' failed, trying next")]
    public static partial void SourceFailed(this ILogger<PluginManager> logger, string sourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "All sources failed for package {packageId}")]
    public static partial void AllSourcesFailed(this ILogger<PluginManager> logger, string packageId);

    // Package search logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Searching for packages matching '{searchTerm}' in {sourceCount} source(s)")]
    public static partial void SearchingPackages(this ILogger<PluginManager> logger, string searchTerm, int sourceCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search resource not available for source '{sourceName}'")]
    public static partial void SearchResourceNotAvailable(this ILogger<PluginManager> logger, string sourceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search completed for source '{sourceName}': {packageCount} package(s) found")]
    public static partial void SearchSourceCompleted(this ILogger<PluginManager> logger, string sourceName, int packageCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search failed for source '{sourceName}'")]
    public static partial void SearchSourceFailed(this ILogger<PluginManager> logger, string sourceName, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Search for '{searchTerm}' completed: {totalCount} package(s) found")]
    public static partial void SearchCompleted(this ILogger<PluginManager> logger, string searchTerm, int totalCount);
}

