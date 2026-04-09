using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Projects.Services;

/// <summary>
/// Default implementation of <see cref="IProjectService"/>.
/// UI-free — manages project folders in standalone mode.
/// </summary>
internal sealed partial class ProjectService(
    IOptions<ProjectEnvironment> projectEnvironment,
    ILogger<ProjectService> logger) : IProjectService
{
    private static string ProjectsDirectory => ConfigPathResolver.ProjectsDirectory;

    /// <inheritdoc />
    public ProjectListResult List()
    {
        var projects = new List<ProjectInfo>();
        var currentPath = projectEnvironment.Value.Path;

        if (Directory.Exists(ProjectsDirectory))
        {
            foreach (var dir in Directory.EnumerateDirectories(ProjectsDirectory))
            {
                var folderName = Path.GetFileName(dir);
                var projectFile = Path.Combine(dir, "project.json");
                var isConfigured = File.Exists(projectFile);
                var displayName = isConfigured ? GetProjectName(projectFile, folderName) : folderName;

                projects.Add(new ProjectInfo
                {
                    FolderName = folderName,
                    Path = dir,
                    DisplayName = displayName,
                    IsConfigured = isConfigured,
                    IsActive = string.Equals(dir, currentPath, StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        return new ProjectListResult
        {
            Projects = projects,
            ProjectsDirectory = ProjectsDirectory
        };
    }

    /// <inheritdoc />
    public ProjectCreateResult Create(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return new ProjectCreateResult { Success = false, ErrorMessage = "Folder name cannot be empty." };
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (folderName.Any(c => invalidChars.Contains(c)))
        {
            return new ProjectCreateResult { Success = false, ErrorMessage = "Folder name contains invalid characters." };
        }

        var targetPath = Path.Combine(ProjectsDirectory, folderName);
        if (Directory.Exists(targetPath))
        {
            return new ProjectCreateResult { Success = false, ErrorMessage = $"Folder '{folderName}' already exists." };
        }

        Directory.CreateDirectory(targetPath);
        LogCreated(logger, folderName, targetPath);

        return new ProjectCreateResult { Success = true, Path = targetPath };
    }

    /// <inheritdoc />
    public ProjectDeleteResult Delete(string folderName)
    {
        var targetPath = Path.Combine(ProjectsDirectory, folderName);
        if (!Directory.Exists(targetPath))
        {
            return new ProjectDeleteResult { Success = false, ErrorMessage = $"Project folder '{folderName}' not found." };
        }

        var currentPath = projectEnvironment.Value.Path;
        var isActive = string.Equals(targetPath, currentPath, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(targetPath, recursive: true);
        LogDeleted(logger, folderName);

        return new ProjectDeleteResult { Success = true, WasActiveProject = isActive };
    }

    private static string GetProjectName(string projectJsonPath, string fallback)
    {
        try
        {
            var json = File.ReadAllText(projectJsonPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("project", out var projectSection)
                && projectSection.TryGetProperty("name", out var nameElement))
            {
                return nameElement.GetString() ?? fallback;
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return fallback;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created project folder '{FolderName}' at {Path}")]
    private static partial void LogCreated(ILogger logger, string folderName, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted project folder '{FolderName}'")]
    private static partial void LogDeleted(ILogger logger, string folderName);
}
