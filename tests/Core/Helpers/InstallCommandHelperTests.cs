using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Models;
using Spectre.Console;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
public sealed class InstallCommandHelperTests
{
    [TestMethod]
    public void FormatPackageChoice_MarkupInMetadata_IsRenderedLiterallyAndKeepsIdFirst()
    {
        var entry = new PackageIndexEntry
        {
            Id = "Spectara.Revela.Plugins.X",
            Version = "1.0.0-[beta]",
            Description = "[/][link=https://evil.test]click",
            Source = "test"
        };

        var choice = InstallCommandHelper.FormatPackageChoice(entry);

        var plain = Markup.Remove(choice);
        Assert.Contains("[/][link=https://evil.test]click", plain, StringComparison.Ordinal);
        Assert.Contains("1.0.0-[beta]", plain, StringComparison.Ordinal);
        Assert.AreEqual("Spectara.Revela.Plugins.X", choice.Split(' ')[0]);
    }
}
