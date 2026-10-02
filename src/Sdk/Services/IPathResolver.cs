namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Resolves source and output paths for the current project.
/// </summary>
/// <remarks>
/// <para>
/// This service dynamically resolves paths from <see cref="Configuration.PathsConfig"/> on
/// every access, so a path changed in-process (e.g. by <c>revela config paths</c> in the
/// interactive menu) is reflected immediately.
/// </para>
/// <para>
/// Paths can be:
/// <list type="bullet">
/// <item>Relative to project root (e.g., "source", "../photos")</item>
/// <item>Absolute paths (e.g., "D:\OneDrive\Photos")</item>
/// </list>
/// </para>
/// <para>
/// Only site files belong in <see cref="OutputPath"/>: everything there is published. Revela core
/// and each package keep their own files in a fixed, non-configurable owner folder:
/// <see cref="ProjectEnvironment.Path"/> combined with <see cref="ProjectPaths.GetOwnerDirectory"/>.
/// </para>
/// </remarks>
public interface IPathResolver
{
    /// <summary>
    /// Gets the resolved absolute path to the source directory.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="Configuration.PathsConfig.Source"/>.
    /// </remarks>
    string SourcePath { get; }

    /// <summary>
    /// Gets the resolved absolute path to the output directory.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="Configuration.PathsConfig.Output"/>.
    /// </remarks>
    string OutputPath { get; }
}
