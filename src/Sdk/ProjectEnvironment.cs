namespace Spectara.Revela.Sdk;

/// <summary>
/// Runtime environment information about the current project.
/// </summary>
/// <remarks>
/// <para>
/// Unlike ProjectConfig which is loaded from project.json,
/// this class contains runtime information about WHERE the project is located.
/// </para>
/// <para>
/// Values are set at startup and don't change during the application lifetime.
/// </para>
/// <para>
/// This is in the SDK so plugins can access it via IOptions&lt;ProjectEnvironment&gt;.
/// </para>
/// <para>
/// For resolved source/output paths, use <see cref="Services.IPathResolver"/> instead.
/// That service supports hot-reload when configuration changes during a session.
/// </para>
/// </remarks>
public sealed class ProjectEnvironment
{
    /// <summary>
    /// Full path to the project directory.
    /// </summary>
    /// <remarks>
    /// This is the ContentRootPath from the host environment.
    /// In standalone mode, this is projects/{folder-name}/.
    /// In tool mode, this is the current working directory.
    /// </remarks>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The folder name of the project (last segment of Path).
    /// </summary>
    public string FolderName => string.IsNullOrEmpty(Path)
        ? string.Empty
        : System.IO.Path.GetFileName(Path);

    /// <summary>
    /// Whether the project has been initialized (has project.json).
    /// </summary>
    public bool IsInitialized => !string.IsNullOrEmpty(Path)
        && File.Exists(System.IO.Path.Combine(Path, "project.json"));
}
