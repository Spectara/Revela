using System.Globalization;
using NSubstitute;
using Spectara.Revela.Commands.Generate.Services;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Commands.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ScribanTemplateEngineTests
{
    [TestMethod]
    public void Render_ShouldBeThreadSafe_ForConcurrentInvocations()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ScribanTemplateEngine>>();
        var markdown = new MarkdownService();
        var resolver = Substitute.For<ITemplateResolver>();
        var engine = new ScribanTemplateEngine(logger, markdown, resolver);

        const string template = "Hello {{ name }}!";
        var model = new { name = "World" };

        // Act
        var outputs = new string[100];
        Parallel.For(0, outputs.Length, i => outputs[i] = engine.Render(template, model));

        // Assert
        foreach (var output in outputs)
        {
            Assert.AreEqual("Hello World!", output);
        }
    }

    [TestMethod]
    public void FormatFileSize_ShouldUseInvariantCulture()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ScribanTemplateEngine>>();
        var markdown = new MarkdownService();
        var resolver = Substitute.For<ITemplateResolver>();
        var engine = new ScribanTemplateEngine(logger, markdown, resolver);
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            const string template = "{{ format_filesize 1048576 }}"; // 1 MB
            var result = engine.Render(template, new { });

            // Assert
            Assert.AreEqual("1 MB", result.Trim());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
