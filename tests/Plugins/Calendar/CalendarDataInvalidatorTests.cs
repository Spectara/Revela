using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Calendar;
using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarDataInvalidatorTests
{
    [TestMethod]
    public async Task InvalidateAsync_PageDataFilesExist_DeletesEveryJsonDataFileOnly()
    {
        using var project = TestProject.CreateMinimal();
        var ownerPath = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("calendar"));
        var pageCache = Path.Combine(ownerPath, "availability");
        Directory.CreateDirectory(pageCache);
        var calendarPath = Path.Combine(pageCache, "calendar.json");
        // A page may name its data file (data.calendar = "rentals.json"); it is calendar data too.
        var customPath = Path.Combine(ownerPath, "rentals.json");
        var unrelatedPath = Path.Combine(pageCache, "notes.txt");
        await File.WriteAllTextAsync(calendarPath, "{}");
        await File.WriteAllTextAsync(customPath, "{}");
        await File.WriteAllTextAsync(unrelatedPath, "keep");
        var invalidator = CreateInvalidator(project.RootPath);

        var result = await invalidator.InvalidateAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(CalendarArtifacts.Data, invalidator.Artifact);
        CollectionAssert.AreEqual(new[] { CoreArtifacts.Manifest }, invalidator.DependsOn.ToArray());
        Assert.IsFalse(File.Exists(calendarPath));
        Assert.IsFalse(File.Exists(customPath));
        Assert.IsTrue(File.Exists(unrelatedPath));
    }

    [TestMethod]
    public async Task InvalidateAsync_DirectoryLinkLeavesCache_PreservesExternalCalendar()
    {
        using var project = TestProject.CreateMinimal();
        var cachePath = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("calendar"));
        Directory.CreateDirectory(cachePath);
        var external = project.RootPath + "-external";
        Directory.CreateDirectory(external);
        var externalCalendar = Path.Combine(external, "calendar.json");
        await File.WriteAllTextAsync(externalCalendar, "{}");
        var link = Path.Combine(cachePath, "linked");
        DirectoryLinkTestHelper.Create(link, external);

        try
        {
            var result = await CreateInvalidator(project.RootPath).InvalidateAsync();

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.IsTrue(File.Exists(externalCalendar));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
            Directory.Delete(external, recursive: true);
        }
    }

    private static CalendarDataInvalidator CreateInvalidator(string projectPath) =>
        new(Options.Create(new ProjectEnvironment { Path = projectPath }));
}
