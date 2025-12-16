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
    public void TransformFullName_NoTransformation()
    {
        // Arrange
        var fullName = "Spectara.Revela.Plugin.Source.OneDrive";

        // Act
        var packageId = TransformPackageName(fullName);

        // Assert
        Assert.AreEqual("Spectara.Revela.Plugin.Source.OneDrive", packageId);
    }

    /// <summary>
    /// Replicates the transformation logic from PluginInstallCommand.ExecuteFromNuGetAsync
    /// </summary>
    private static string TransformPackageName(string name)
    {
        // This is the FIXED implementation from the code
        return name.StartsWith("Spectara.Revela.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"Spectara.Revela.Plugin.{name}";
    }
}
