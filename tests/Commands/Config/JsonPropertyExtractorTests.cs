using System.Text.Json.Nodes;

using Spectara.Revela.Commands.Config.Services;

namespace Spectara.Revela.Tests.Commands.Config;

[TestClass]
[TestCategory("Unit")]
public sealed class JsonPropertyExtractorTests
{
    private const string Template = /*lang=json,strict*/ """
        { "title": "", "author": "", "social": { "instagram": "" }, "items": 3 }
        """;

    [TestMethod]
    public void BuildJson_NoExistingDocument_UsesTemplateStructureAndValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = "My Site",
            ["social.instagram"] = "@me",
        };

        var result = JsonPropertyExtractor.BuildJson(Template, existingJson: null, values);

        var expected = JsonNode.Parse("""
            { "title": "My Site", "author": "", "social": { "instagram": "@me" }, "items": 3 }
            """);
        Assert.IsTrue(JsonNode.DeepEquals(expected, result), result.ToJsonString());
    }

    [TestMethod]
    public void BuildJson_ExistingKeysNotInTemplate_ArePreservedAfterTemplateKeys()
    {
        const string existing = /*lang=json,strict*/ """
            { "language": "de", "title": "Old", "social": { "mastodon": "@me@example.social" }, "extra": [1, null, { "x": true }] }
            """;

        var result = JsonPropertyExtractor.BuildJson(Template, existing, new Dictionary<string, string>());

        var expected = JsonNode.Parse("""
            {
              "title": "Old", "author": "", "social": { "instagram": "", "mastodon": "@me@example.social" }, "items": 3,
              "language": "de", "extra": [1, null, { "x": true }]
            }
            """);
        Assert.IsTrue(JsonNode.DeepEquals(expected, result), result.ToJsonString());
        Assert.AreEqual("title", result.First().Key, "Template order comes first.");
    }

    [TestMethod]
    public void BuildJson_PromptedValues_ReplaceStringLeavesIncludingExistingOnlyKeys()
    {
        const string existing = /*lang=json,strict*/ """{ "title": "Old", "language": "de", "author": null, "items": 7 }""";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = "New",
            ["language"] = "en",
            ["author"] = "Jane",
            ["items"] = "9",
        };

        var result = JsonPropertyExtractor.BuildJson(Template, existing, values);

        Assert.AreEqual("New", result["title"]?.GetValue<string>());
        Assert.AreEqual("en", result["language"]?.GetValue<string>());
        Assert.AreEqual("Jane", result["author"]?.GetValue<string>());
        Assert.AreEqual(7, result["items"]?.GetValue<int>(), "Non-string values keep their type and existing value.");
    }

    [TestMethod]
    public void BuildJson_ExistingWithComments_IsReadLeniently()
    {
        const string existing = /*lang=json*/ """
            {
              // kept by the reader, dropped on write
              "language": "fr",
            }
            """;

        var result = JsonPropertyExtractor.BuildJson(Template, existing, new Dictionary<string, string>());

        Assert.AreEqual("fr", result["language"]?.GetValue<string>());
    }

    [TestMethod]
    public void ExtractProperties_NestedAndScalarLeaves_ReturnsDottedPaths()
    {
        var properties = JsonPropertyExtractor.ExtractProperties(/*lang=json,strict*/ """
            { "title": "T", "social": { "x": "@x" }, "count": 2, "enabled": false, "none": null, "list": [1] }
            """);

        Assert.AreEqual("title,social.x,count,enabled,none", string.Join(',', properties.Select(p => p.Path)));
        Assert.AreEqual("@x", properties[1].Value);
        Assert.AreEqual("false", properties[3].Value);
    }
}
