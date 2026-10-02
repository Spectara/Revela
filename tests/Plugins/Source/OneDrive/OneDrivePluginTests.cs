using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugins.Source.OneDrive;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive;

[TestClass]
[TestCategory("Unit")]
public sealed class OneDrivePluginTests
{
    [TestMethod]
    public void ConfigureServices_CalledTwice_RegistersConfigValidatorOnce()
    {
        var plugin = new OneDrivePlugin();
        var services = new ServiceCollection();

        plugin.ConfigureServices(services);
        plugin.ConfigureServices(services);

        Assert.HasCount(1, services.Where(d => d.ServiceType == typeof(IValidateOptions<OneDrivePluginConfig>)));
    }
}
