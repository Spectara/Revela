using System.Text.Json;

namespace Spectara.Revela.Features.Restore;

/// <summary>
/// Represents a required dependency discovered from project configuration
/// </summary>
public sealed record RequiredDependency
{
    /// <summary>
    /// Package identifier (e.g., "Spectara.Revela.Plugin.Source.OneDrive")
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Optional version constraint (e.g., "1.2.0", ">=1.0.0")
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Type of dependency
    /// </summary>
    public required DependencyType Type { get; init; }

    /// <summary>
    /// Source file where dependency was declared
    /// </summary>
    public required string SourceFile { get; init; }
}

/// <summary>
/// Type of dependency
/// </summary>
public enum DependencyType
{
    /// <summary>Theme package</summary>
    Theme,

    /// <summary>Plugin package</summary>
    Plugin
}

/// <summary>
/// Scans project directory for required dependencies
/// </summary>
/// <remarks>
/// Discovers dependencies from:
/// - project.json: "theme" property
/// - plugins/*.json: "$plugin" property in each file
/// </remarks>
public interface IDependencyScanner
{
    /// <summary>
    /// Scan project for required dependencies
    /// </summary>
    /// <param name="projectPath">Path to project directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of required dependencies</returns>
    Task<IReadOnlyList<RequiredDependency>> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of dependency scanner
/// </summary>
public sealed partial class DependencyScanner(ILogger<DependencyScanner> logger) : IDependencyScanner
{
    private const string ProjectJsonFileName = "project.json";
    private const string PluginsFolderName = "plugins";
    private const string PluginPropertyName = "$plugin";
    private const string ThemePropertyName = "theme";
    private const string ThemePackagePrefix = "Spectara.Revela.Theme.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequiredDependency>> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var dependencies = new List<RequiredDependency>();

        // 1. Scan project.json for theme
        var themeDep = await ScanProjectJsonAsync(projectPath, cancellationToken);
        if (themeDep != null)
        {
            dependencies.Add(themeDep);
        }

        // 2. Scan plugins/ folder for plugin configs
        var pluginDeps = await ScanPluginsFolderAsync(projectPath, cancellationToken);
        dependencies.AddRange(pluginDeps);

        LogDependenciesFound(dependencies.Count, projectPath);
        return dependencies;
    }

    private async Task<RequiredDependency?> ScanProjectJsonAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var projectJsonPath = Path.Combine(projectPath, ProjectJsonFileName);
        if (!File.Exists(projectJsonPath))
        {
            LogProjectJsonNotFound(projectPath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(projectJsonPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty(ThemePropertyName, out var themeElement))
            {
                var themeName = themeElement.GetString();
                if (!string.IsNullOrEmpty(themeName))
                {
                    // Parse version if specified (e.g., "Fancy@1.2.0")
                    var (name, version) = ParsePackageSpec(themeName);

                    // Convert theme name to package ID
                    var packageId = name.StartsWith(ThemePackagePrefix, StringComparison.OrdinalIgnoreCase)
                        ? name
                        : ThemePackagePrefix + name;

                    LogThemeFound(name, projectJsonPath);
                    return new RequiredDependency
                    {
                        PackageId = packageId,
                        Version = version,
                        Type = DependencyType.Theme,
                        SourceFile = projectJsonPath
                    };
                }
            }
        }
        catch (JsonException ex)
        {
            LogJsonParseError(projectJsonPath, ex.Message);
        }

        return null;
    }

    private async Task<IReadOnlyList<RequiredDependency>> ScanPluginsFolderAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var dependencies = new List<RequiredDependency>();
        var pluginsPath = Path.Combine(projectPath, PluginsFolderName);

        if (!Directory.Exists(pluginsPath))
        {
            LogPluginsFolderNotFound(projectPath);
            return dependencies;
        }

        var jsonFiles = Directory.GetFiles(pluginsPath, "*.json");
        LogScanningPluginsFolder(pluginsPath, jsonFiles.Length);

        foreach (var jsonFile in jsonFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(jsonFile, cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty(PluginPropertyName, out var pluginElement))
                {
                    var pluginSpec = pluginElement.GetString();
                    if (!string.IsNullOrEmpty(pluginSpec))
                    {
                        var (packageId, version) = ParsePackageSpec(pluginSpec);

                        LogPluginFound(packageId, jsonFile);
                        dependencies.Add(new RequiredDependency
                        {
                            PackageId = packageId,
                            Version = version,
                            Type = DependencyType.Plugin,
                            SourceFile = jsonFile
                        });
                    }
                }
                else
                {
                    LogMissingPluginProperty(jsonFile);
                }
            }
            catch (JsonException ex)
            {
                LogJsonParseError(jsonFile, ex.Message);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Parse package specification with optional version
    /// </summary>
    /// <example>
    /// "MyPackage" → ("MyPackage", null)
    /// "MyPackage@1.2.0" → ("MyPackage", "1.2.0")
    /// </example>
    private static (string Name, string? Version) ParsePackageSpec(string spec)
    {
        var atIndex = spec.LastIndexOf('@');
        if (atIndex > 0 && atIndex < spec.Length - 1)
        {
            return (spec[..atIndex], spec[(atIndex + 1)..]);
        }

        return (spec, null);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "project.json not found in {ProjectPath}")]
    private partial void LogProjectJsonNotFound(string projectPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "plugins/ folder not found in {ProjectPath}")]
    private partial void LogPluginsFolderNotFound(string projectPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanning plugins folder {PluginsPath}: {Count} JSON file(s)")]
    private partial void LogScanningPluginsFolder(string pluginsPath, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found theme '{ThemeName}' in {SourceFile}")]
    private partial void LogThemeFound(string themeName, string sourceFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found plugin '{PackageId}' in {SourceFile}")]
    private partial void LogPluginFound(string packageId, string sourceFile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JSON file {FilePath} is missing '$plugin' property")]
    private partial void LogMissingPluginProperty(string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse JSON in {FilePath}: {Error}")]
    private partial void LogJsonParseError(string filePath, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} dependency(ies) in {ProjectPath}")]
    private partial void LogDependenciesFound(int count, string projectPath);
}
