using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Service for theme management operations — list, install, uninstall, file inspection.
/// </summary>
/// <remarks>
/// UI-free service for use by CLI, MCP, GUI, or other consumers.
/// CLI commands are thin wrappers that format the results with Spectre.Console.
/// </remarks>
public interface IThemeService
{
    /// <summary>
    /// Lists installed and optionally available online themes.
    /// </summary>
    Task<ThemeListResult> ListAsync(
        bool includeOnline = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently configured theme info.
    /// </summary>
    ThemeInfoResult GetCurrentTheme();

    /// <summary>
    /// Installs a theme by name or package ID.
    /// </summary>
    Task<bool> InstallAsync(
        string name,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a theme by name or package ID.
    /// </summary>
    Task<bool> UninstallAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resolved files (templates + assets) for a theme with source tracking.
    /// </summary>
    ThemeFilesResult GetFiles(string? themeName = null);

    /// <summary>
    /// Switches the active theme in project.json.
    /// </summary>
    Task SetActiveThemeAsync(
        string themeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a theme (and its extensions) to the local themes folder.
    /// </summary>
    /// <param name="sourceName">Theme name to extract.</param>
    /// <param name="targetName">Optional rename (null = same as source).</param>
    /// <param name="force">Overwrite existing files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with extracted path and extension list.</returns>
    Task<ThemeExtractResult> ExtractAsync(
        string sourceName,
        string? targetName = null,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts specific files from a theme to the local themes folder.
    /// </summary>
    /// <param name="filePattern">File or folder pattern (e.g., "Body/Gallery.revela" or "Partials/").</param>
    /// <param name="force">Overwrite existing files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with list of extracted file paths.</returns>
    Task<ThemeExtractResult> ExtractFilesAsync(
        string filePattern,
        bool force = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of listing themes.
/// </summary>
public sealed class ThemeListResult
{
    /// <summary>Installed themes (local + NuGet).</summary>
    public required IReadOnlyList<ThemeInfo> Installed { get; init; }

    /// <summary>Online themes available for installation (only when requested).</summary>
    public IReadOnlyList<OnlineThemeInfo> Online { get; init; } = [];
}

/// <summary>
/// Information about an installed theme.
/// </summary>
public sealed class ThemeInfo
{
    /// <summary>Theme metadata.</summary>
    public required PackageMetadata Metadata { get; init; }

    /// <summary>Whether this is a local theme (from project/themes/).</summary>
    public required bool IsLocal { get; init; }

    /// <summary>Package source (Bundled, Local) — null for local themes.</summary>
    public PackageSource? Source { get; init; }

    /// <summary>Extensions targeting this theme.</summary>
    public IReadOnlyList<ThemeExtensionInfo> Extensions { get; init; } = [];
}

/// <summary>
/// Information about a theme extension.
/// </summary>
public sealed class ThemeExtensionInfo
{
    /// <summary>Extension metadata.</summary>
    public required PackageMetadata Metadata { get; init; }

    /// <summary>Package source.</summary>
    public PackageSource? Source { get; init; }
}

/// <summary>
/// Information about an online theme available for installation.
/// </summary>
public sealed class OnlineThemeInfo
{
    /// <summary>Package ID.</summary>
    public required string Id { get; init; }

    /// <summary>Display name (extracted from package ID).</summary>
    public required string Name { get; init; }

    /// <summary>Latest version.</summary>
    public required string Version { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether already installed.</summary>
    public required bool IsInstalled { get; init; }
}

/// <summary>
/// Result of getting current theme info.
/// </summary>
public sealed class ThemeInfoResult
{
    /// <summary>Configured theme name.</summary>
    public required string ThemeName { get; init; }

    /// <summary>Resolved theme (null if not found).</summary>
    public ITheme? Theme { get; init; }

    /// <summary>Source type (local, bundled, installed).</summary>
    public required string Source { get; init; }

    /// <summary>Extensions for this theme.</summary>
    public IReadOnlyList<ThemeExtensionInfo> Extensions { get; init; } = [];
}

/// <summary>
/// Result of inspecting theme files.
/// </summary>
public sealed class ThemeFilesResult
{
    /// <summary>Resolved template files.</summary>
    public required IReadOnlyList<ResolvedFileInfo> Templates { get; init; }

    /// <summary>Resolved asset files.</summary>
    public required IReadOnlyList<ResolvedFileInfo> Assets { get; init; }

    /// <summary>Theme name.</summary>
    public required string ThemeName { get; init; }
}

/// <summary>
/// Result of extracting a theme.
/// </summary>
public sealed class ThemeExtractResult
{
    /// <summary>Whether extraction succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Target directory path.</summary>
    public string? TargetPath { get; init; }

    /// <summary>Theme name used.</summary>
    public string? ThemeName { get; init; }

    /// <summary>Extracted extension names (PascalCase folder names).</summary>
    public IReadOnlyList<string> ExtractedExtensions { get; init; } = [];

    /// <summary>Extracted file paths (for selective extraction).</summary>
    public IReadOnlyList<string> ExtractedFiles { get; init; } = [];

    /// <summary>Error message if extraction failed.</summary>
    public string? ErrorMessage { get; init; }
}
