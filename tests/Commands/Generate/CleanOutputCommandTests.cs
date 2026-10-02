using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Generate;

[TestClass]
[TestCategory("Integration")]
public sealed class CleanOutputCommandTests
{
    [TestMethod]
    [DataRow(".", "source", "")]
    [DataRow("./", "source", "")]
    [DataRow("source", "source", "")]
    [DataRow("source/", "source", "")]
    [DataRow("photos", "photos/library", "")]
    [DataRow("..", "source", "site")]
    public async Task ExecuteAsync_OutputProtectsProjectData_FailsAndDeletesNothing(
        string output,
        string source,
        string projectSubdirectory)
    {
        using var workspace = TestProject.Create();
        var projectPath = CreateProject(workspace.RootPath, projectSubdirectory, output, source);
        var photo = CreateFile(Path.Combine(projectPath, source), "photo.jpg");
        using var host = RevelaTestHost.Build(projectPath, services => services.AddGenerateFeature());
        var command = host.Services.GetRequiredService<CleanOutputCommand>();

        var exitCode = await command.ExecuteAsync(CancellationToken.None);

        Assert.AreEqual(1, exitCode);
        Assert.IsTrue(File.Exists(photo), "Source photo must not be deleted.");
        Assert.IsTrue(File.Exists(Path.Combine(projectPath, "project.json")), "project.json must not be deleted.");
    }

    [TestMethod]
    public async Task PipelineStep_OutputIsProjectRoot_FailsAndDeletesNothing()
    {
        using var workspace = TestProject.Create();
        var projectPath = CreateProject(workspace.RootPath, string.Empty, ".", "source");
        var photo = CreateFile(Path.Combine(projectPath, "source"), "photo.jpg");
        using var host = RevelaTestHost.Build(projectPath, services => services.AddGenerateFeature());
        IPipelineStep step = host.Services.GetRequiredService<CleanOutputCommand>();

        var result = await step.ExecuteAsync(CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.IsTrue(File.Exists(photo), "Source photo must not be deleted.");
    }

    [TestMethod]
    [DataRow("output")]
    [DataRow("dist/site")]
    public async Task ExecuteAsync_OutputInsideProject_DeletesOnlyOutput(string output)
    {
        using var workspace = TestProject.Create();
        var projectPath = CreateProject(workspace.RootPath, string.Empty, output, "source");
        var photo = CreateFile(Path.Combine(projectPath, "source"), "photo.jpg");
        var outputPath = Path.GetFullPath(Path.Combine(projectPath, output));
        _ = CreateFile(outputPath, "index.html");
        using var host = RevelaTestHost.Build(projectPath, services => services.AddGenerateFeature());
        var command = host.Services.GetRequiredService<CleanOutputCommand>();

        var exitCode = await command.ExecuteAsync(CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(Directory.Exists(outputPath));
        Assert.IsTrue(File.Exists(photo));
    }

    private static string CreateProject(string workspaceRoot, string projectSubdirectory, string output, string source)
    {
        var projectPath = Path.Combine(workspaceRoot, projectSubdirectory);
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(
            Path.Combine(projectPath, "project.json"),
            $$"""{ "paths": { "source": "{{source}}", "output": "{{output}}" } }""");
        return projectPath;
    }

    private static string CreateFile(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "data");
        return path;
    }
}
