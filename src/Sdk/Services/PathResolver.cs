using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Resolves source and output paths for the current project.
/// </summary>
/// <remarks>
/// Uses <see cref="IOptionsMonitor{T}"/> for hot-reload support.
/// Paths are resolved dynamically on each access, so configuration
/// changes during a session are immediately reflected.
/// </remarks>
public sealed class PathResolver(
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<PathsConfig> pathsConfig) : IPathResolver
{
    /// <inheritdoc />
    public string SourcePath => ResolvePath(pathsConfig.CurrentValue.Source);

    /// <inheritdoc />
    public string OutputPath => ResolvePath(pathsConfig.CurrentValue.Output);

    /// <summary>
    /// Resolves a configured path to an absolute path.
    /// </summary>
    /// <param name="configuredPath">The path from configuration (relative or absolute).</param>
    /// <returns>The resolved absolute path.</returns>
    private string ResolvePath(string configuredPath)
    {
        var projectPath = projectEnvironment.Value.Path;

        // If the path is already absolute (rooted), use it as-is
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        // Relative path: resolve against project directory
        return Path.GetFullPath(Path.Combine(projectPath, configuredPath));
    }
}
