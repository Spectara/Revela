namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Service for project management operations (standalone mode).
/// </summary>
/// <remarks>
/// UI-free service for use by CLI, MCP, GUI, or other consumers.
/// Only available in standalone mode (not dotnet tool).
/// </remarks>
public interface IProjectService
{
    /// <summary>
    /// Lists all project folders.
    /// </summary>
    ProjectListResult List();

    /// <summary>
    /// Creates a new project folder.
    /// </summary>
    /// <param name="folderName">Folder name to create.</param>
    /// <returns>Result with the created path.</returns>
    ProjectCreateResult Create(string folderName);

    /// <summary>
    /// Deletes a project folder.
    /// </summary>
    /// <param name="folderName">Folder name to delete.</param>
    /// <returns>Result indicating success.</returns>
    ProjectDeleteResult Delete(string folderName);
}

/// <summary>
/// Info about a project folder.
/// </summary>
public sealed class ProjectInfo
{
    /// <summary>Folder name.</summary>
    public required string FolderName { get; init; }

    /// <summary>Full path.</summary>
    public required string Path { get; init; }

    /// <summary>Display name (from project.json or folder name).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether project.json exists.</summary>
    public required bool IsConfigured { get; init; }

    /// <summary>Whether this is the currently active project.</summary>
    public required bool IsActive { get; init; }
}

/// <summary>
/// Result of listing projects.
/// </summary>
public sealed class ProjectListResult
{
    /// <summary>All project folders.</summary>
    public required IReadOnlyList<ProjectInfo> Projects { get; init; }

    /// <summary>Projects directory path.</summary>
    public required string ProjectsDirectory { get; init; }
}

/// <summary>
/// Result of creating a project.
/// </summary>
public sealed class ProjectCreateResult
{
    /// <summary>Whether creation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Created path.</summary>
    public string? Path { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of deleting a project.
/// </summary>
public sealed class ProjectDeleteResult
{
    /// <summary>Whether deletion succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Whether the deleted project was the active one.</summary>
    public bool WasActiveProject { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
