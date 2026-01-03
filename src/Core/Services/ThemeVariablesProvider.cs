using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Provides theme variables with local override support.
/// </summary>
/// <remarks>
/// Resolution order (either-or, no merge):
/// 1. Local override: theme/theme.json → variables section
/// 2. Theme default: IThemePlugin.GetManifest().Variables
/// </remarks>
public interface IThemeVariablesProvider
{
    /// <summary>
    /// Gets the theme variables, checking for local overrides first.
    /// </summary>
    /// <param name="theme">The resolved theme plugin.</param>
    /// <returns>Dictionary of variable name to value.</returns>
    IReadOnlyDictionary<string, string> GetVariables(IThemePlugin? theme);
}

/// <summary>
/// Default implementation of <see cref="IThemeVariablesProvider"/>.
/// </summary>
public sealed partial class ThemeVariablesProvider(
    IOptions<ProjectEnvironment> projectEnvironment,
    ILogger<ThemeVariablesProvider> logger) : IThemeVariablesProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetVariables(IThemePlugin? theme)
    {
        // Try local override first
        var localVariables = TryLoadLocalVariables();
        if (localVariables is not null)
        {
            LogUsingLocalVariables(logger, localVariables.Count);
            return localVariables;
        }

        // Fall back to theme defaults
        var themeVariables = theme?.GetManifest().Variables;
        if (themeVariables is not null && themeVariables.Count > 0)
        {
            LogUsingThemeVariables(logger, themeVariables.Count);
            return themeVariables;
        }

        LogNoVariablesFound(logger);
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Try to load variables from local theme/theme.json file.
    /// </summary>
    private Dictionary<string, string>? TryLoadLocalVariables()
    {
        var projectPath = projectEnvironment.Value.Path;
        var themeJsonPath = Path.Combine(projectPath, "theme", "theme.json");

        if (!File.Exists(themeJsonPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(themeJsonPath);
            var config = JsonSerializer.Deserialize<LocalThemeJson>(json, JsonOptions);

            if (config?.Variables is null || config.Variables.Count == 0)
            {
                LogLocalThemeJsonNoVariables(logger, themeJsonPath);
                return null;
            }

            LogLoadedLocalVariables(logger, themeJsonPath, config.Variables.Count);
            return config.Variables;
        }
        catch (JsonException ex)
        {
            LogLocalThemeJsonParseError(logger, themeJsonPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Minimal model for parsing theme.json - only variables section.
    /// </summary>
    private sealed class LocalThemeJson
    {
        public Dictionary<string, string>? Variables { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using local theme variables ({Count} variables)")]
    private static partial void LogUsingLocalVariables(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using theme default variables ({Count} variables)")]
    private static partial void LogUsingThemeVariables(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No theme variables found")]
    private static partial void LogNoVariablesFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} variables from {Path}")]
    private static partial void LogLoadedLocalVariables(ILogger logger, string path, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Local theme.json at {Path} has no variables section")]
    private static partial void LogLocalThemeJsonNoVariables(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse local theme.json at {Path}: {Error}")]
    private static partial void LogLocalThemeJsonParseError(ILogger logger, string path, string error);
}
