using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Compress;

/// <summary>
/// Where the Compress plugin keeps its ownership record in a test project.
/// </summary>
internal static class CompressTestPaths
{
    public static string StateDirectory(this TestProject project) =>
        Path.Combine(project.RootPath, ".revela", "state");

    public static string OwnershipRecord(this TestProject project) =>
        Path.Combine(project.StateDirectory(), "compress.json");

    public static IOptions<ProjectEnvironment> Environment(this TestProject project) =>
        Options.Create(new ProjectEnvironment { Path = project.RootPath });
}
