using System.CommandLine;
using NSubstitute;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Generate.Commands;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class CreatePageCommandTests
{
    [TestMethod]
    public async Task CreatePage_NoPathNonInteractive_FailsWithHintWithoutWritingPage()
    {
        using var source = new TempDirectory();

        var (exitCode, output) = await ConsoleCapture.InvokeAsync(CreateCommand(source.Path), "booking");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("revela create page booking <path>", output);
        Assert.IsEmpty(Directory.GetFileSystemEntries(source.Path));
    }

    [TestMethod]
    public async Task CreatePage_RequiredPropertyMissing_FailsWithoutWritingPage()
    {
        using var source = new TempDirectory();

        var exitCode = await InvokeAsync(source.Path, "booking", "availability");

        Assert.AreNotEqual(0, exitCode);
        Assert.IsFalse(File.Exists(Path.Combine(source.Path, "availability", "_index.revela")));
    }

    [TestMethod]
    public async Task CreatePage_RequiredPropertyGiven_WritesPage()
    {
        using var source = new TempDirectory();

        var exitCode = await InvokeAsync(source.Path, "booking", "availability", "--source", "bookings.ics");

        Assert.AreEqual(0, exitCode);
        var frontmatter = await File.ReadAllTextAsync(Path.Combine(source.Path, "availability", "_index.revela"));
        Assert.Contains("calendar.source = \"bookings.ics\"", frontmatter);
    }

    private static async Task<int> InvokeAsync(string sourcePath, params string[] args) =>
        await CreateCommand(sourcePath).Parse(args).InvokeAsync(new InvocationConfiguration { Output = TextWriter.Null, Error = TextWriter.Null });

    private static Command CreateCommand(string sourcePath)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(sourcePath);
        return new CreatePageCommand(
            Substitute.For<ILogger<CreatePageCommand>>(),
            pathResolver,
            [new RequiredSourceTemplate()],
            FakeConsoleCapabilities.NonInteractive).Create();
    }

    private sealed class RequiredSourceTemplate : IPageTemplate
    {
        public string Name => "booking";

        public string DisplayName => "Booking";

        public string Description => "Booking page";

        public string TemplateName => "calendar/page";

        public IReadOnlyList<TemplateProperty> PageProperties { get; } =
        [
            new()
            {
                Name = "source",
                Aliases = ["--source"],
                Type = typeof(string),
                DefaultValue = "",
                Description = "Calendar source",
                Required = true,
                FrontmatterKey = "calendar.source"
            }
        ];
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("revela-create-page-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
