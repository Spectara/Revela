using Spectara.Revela.Plugin.Serve.Configuration;

namespace Spectara.Revela.Plugin.Serve.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ServePluginConfigTests
{
    [TestMethod]
    public void DefaultPort_Is8080()
    {
        var config = new ServePluginConfig();
        Assert.AreEqual(8080, config.Port);
    }

    [TestMethod]
    public void DefaultVerbose_IsFalse()
    {
        var config = new ServePluginConfig();
        Assert.IsFalse(config.Verbose);
    }
}
