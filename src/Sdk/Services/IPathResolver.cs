namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Resolves source and output paths for the current project.
/// </summary>
/// <remarks>
/// <para>
/// This service dynamically resolves paths from <see cref="Configuration.PathsConfig"/>,
/// supporting hot-reload when configuration changes during a session.
/// </para>
/// <para>
/// Paths can be:
/// <list type="bullet">
/// <item>Relative to project root (e.g., "source", "../photos")</item>
/// <item>Absolute paths (e.g., "D:\OneDrive\Photos")</item>
/// </list>
/// </para>
/// </remarks>
public interface IPathResolver
{
    /// <summary>
    /// Gets the resolved absolute path to the source directory.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="Configuration.PathsConfig.Source"/>.
    /// Supports hot-reload when configuration changes.
    /// </remarks>
    string SourcePath { get; }

    /// <summary>
    /// Gets the resolved absolute path to the output directory.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="Configuration.PathsConfig.Output"/>.
    /// Supports hot-reload when configuration changes.
    /// </remarks>
    string OutputPath { get; }
}
