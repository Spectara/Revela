using Spectara.Revela.Plugin.Serve.Configuration;

namespace Spectara.Revela.Plugin.Serve.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ServePluginConfigTests
{
    [TestMethod]
    public void DefaultPort_Is8080()
    {
        // Arrange & Act
        var config = new ServePluginConfig();

        // Assert
        Assert.AreEqual(8080, config.Port);
    }

    [TestMethod]
    public void DefaultVerbose_IsFalse()
    {
        // Arrange & Act
        var config = new ServePluginConfig();

        // Assert
        Assert.IsFalse(config.Verbose);
    }

    [TestMethod]
    public void Port_CanBeCustomized()
    {
        // Arrange & Act
        var config = new ServePluginConfig { Port = 3000 };

        // Assert
        Assert.AreEqual(3000, config.Port);
    }

    [TestMethod]
    public void Verbose_CanBeEnabled()
    {
        // Arrange & Act
        var config = new ServePluginConfig { Verbose = true };

        // Assert
        Assert.IsTrue(config.Verbose);
    }
}
