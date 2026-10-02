using Spectara.Revela.Core.Models;
using Spectara.Revela.Features.Packages.Commands.Revela;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Unit")]
public sealed class WizardTests
{
    private static readonly string[] ExpectedThemes = ["Spectara.Revela.Themes.Lumina"];
    private static readonly string[] ExpectedPlugins = ["Spectara.Revela.Plugins.Statistics", "spectara.revela.plugins.lowercase"];

    [TestMethod]
    public void PartitionPackages_MixedIndex_OffersOnlyOfficialPackages()
    {
        IReadOnlyList<PackageIndexEntry> themes = [Entry("Spectara.Revela.Themes.Lumina"), Entry("Evil.Themes.Fake")];
        IReadOnlyList<PackageIndexEntry> plugins =
        [
            Entry("Evil.Plugins.Core.Backdoor"),
            Entry("Evil.Spectara.Revela.Plugins.Squat"),
            Entry("Spectara.Revela.Plugins.Statistics"),
            Entry("spectara.revela.plugins.lowercase"),
        ];

        var (offeredThemes, offeredPlugins) = Wizard.PartitionPackages(themes, plugins);

        CollectionAssert.AreEqual(ExpectedThemes, offeredThemes.Select(p => p.Id).ToArray());
        CollectionAssert.AreEqual(ExpectedPlugins, offeredPlugins.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void FormatChoiceLabel_DescriptionWithMarkup_IsRenderedLiterally(bool isTheme)
    {
        var entry = Entry(isTheme ? "Spectara.Revela.Themes.X" : "Spectara.Revela.Plugins.X", "[/][red]boom");

        var label = Wizard.FormatChoiceLabel(entry, isTheme);

        Assert.Contains("[/][red]boom", Markup.Remove(label), StringComparison.Ordinal);
    }

    private static PackageIndexEntry Entry(string id, string description = "Test") =>
        new() { Id = id, Version = "1.0.0", Description = description, Source = "test", Types = ["RevelaPlugin"] };
}
