using Spectara.Revela.Plugin.Serve.Configuration;

namespace Spectara.Revela.Plugin.Serve.Tests;

[TestClass]
public sealed class ServeConfigTests
{
    [TestMethod]
    public void DefaultPort_Is8080()
    {
        // Arrange & Act
        var config = new ServeConfig();

        // Assert
        Assert.AreEqual(8080, config.Port);
    }

    [TestMethod]
    public void DefaultVerbose_IsFalse()
    {
        // Arrange & Act
        var config = new ServeConfig();

        // Assert
        Assert.IsFalse(config.Verbose);
    }

    [TestMethod]
    public void SectionName_StartsWithPluginNamespace() =>
        // Assert - verify section name follows plugin naming convention
        Assert.IsTrue(ServeConfig.SectionName.StartsWith("Spectara.Revela.Plugin.", StringComparison.Ordinal));

    [TestMethod]
    public void Port_CanBeCustomized()
    {
        // Arrange & Act
        var config = new ServeConfig { Port = 3000 };

        // Assert
        Assert.AreEqual(3000, config.Port);
    }

    [TestMethod]
    public void Verbose_CanBeEnabled()
    {
        // Arrange & Act
        var config = new ServeConfig { Verbose = true };

        // Assert
        Assert.IsTrue(config.Verbose);
    }
}
