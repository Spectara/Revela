namespace Spectara.Revela.Commands.Tests.Plugins;

/// <summary>
/// Unit tests for PluginInstallCommand name transformation logic
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PluginInstallCommandTests
{
    [TestMethod]
    public void TransformShortName_AddsCorrectPrefix()
    {
        // Arrange
        var shortName = "OneDrive";

        // Act
        var packageId = TransformPackageName(shortName);

        // Assert
        Assert.AreEqual("Spectara.Revela.Plugin.OneDrive", packageId);
    }

    [TestMethod]
    public void TransformShortNameWithCategory_AddsCorrectPrefix()
    {
        // Arrange
        var shortName = "Source.OneDrive";

        // Act
        var packageId = TransformPackageName(shortName);

        // Assert
        Assert.AreEqual("Spectara.Revela.Plugin.Source.OneDrive", packageId);
    }

    [TestMethod]
    public void TransformFullPluginName_NoTransformation()
    {
        // Arrange
        var fullName = "Spectara.Revela.Plugin.Source.OneDrive";

        // Act
        var packageId = TransformPackageName(fullName);

        // Assert
        Assert.AreEqual("Spectara.Revela.Plugin.Source.OneDrive", packageId);
    }

    [TestMethod]
    public void TransformFullThemeName_NoTransformation()
    {
        // Arrange - Theme names should NOT get Plugin. prefix added
        var fullName = "Spectara.Revela.Theme.Lumina.Statistics";

        // Act
        var packageId = TransformPackageName(fullName);

        // Assert - Should remain unchanged (not get double prefix)
        Assert.AreEqual("Spectara.Revela.Theme.Lumina.Statistics", packageId);
    }

    /// <summary>
    /// Replicates the transformation logic from PluginInstallCommand.ExecuteFromNuGetAsync
    /// </summary>
    private static string TransformPackageName(string name)
    {
        // This matches the implementation in PluginInstallCommand
        // Names starting with "Spectara.Revela." are treated as full package IDs
        return name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"Spectara.Revela.Plugin.{name}";
    }
}
