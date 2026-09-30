using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Commands.Generate.Commands;

[TestClass]
[TestCategory("Unit")]
public sealed class CreatePageFrontmatterTests
{
    [TestMethod]
    public void GenerateFrontmatter_PluginDottedKeys_WritesTemplateFirstAndParsesBack()
    {
        var values = new Dictionary<string, object?> { ["source"] = "bookings.ics", ["months"] = 6 };

        var frontmatter = CreatePageCommand.GenerateFrontmatter(new CalendarLikeTemplate(), values);

        var lines = frontmatter.ReplaceLineEndings("\n").Split('\n');
        Assert.AreEqual("+++", lines[0]);
        Assert.AreEqual("template = \"calendar/page\"", lines[1]);
        Assert.Contains("calendar.source = \"bookings.ics\"", frontmatter);
        Assert.AreEqual("calendar/page", RevelaParser.Parse(frontmatter).Template);
    }

    private sealed class CalendarLikeTemplate : IPageTemplate
    {
        public string Name => "calendar";

        public string DisplayName => "Calendar";

        public string Description => "Calendar page";

        public string TemplateName => "calendar/page";

        public IReadOnlyList<TemplateProperty> PageProperties { get; } =
        [
            new()
            {
                Name = "source",
                Aliases = ["--source"],
                Type = typeof(string),
                DefaultValue = "",
                Description = "Source",
                FrontmatterKey = "calendar.source"
            },
            new()
            {
                Name = "months",
                Aliases = ["--months"],
                Type = typeof(int),
                DefaultValue = 12,
                Description = "Months",
                FrontmatterKey = "calendar.months"
            },
        ];
    }
}
