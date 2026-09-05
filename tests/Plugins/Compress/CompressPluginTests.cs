using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugins.Compress;
using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Tests.Plugins.Compress;

[TestClass]
[TestCategory("Unit")]
public sealed class CompressPluginTests
{
    [TestMethod]
    public void ConfigureServices_RegistersCompressedSiteInvalidator()
    {
        var plugin = new CompressPlugin();
        var services = new ServiceCollection();

        plugin.ConfigureServices(services);

        var descriptor = services.SingleOrDefault(item =>
            item.ServiceType == typeof(IArtifactInvalidator));
        Assert.IsNotNull(descriptor);
        Assert.AreEqual(typeof(CompressedSiteInvalidator), descriptor.ImplementationType);
    }
}
