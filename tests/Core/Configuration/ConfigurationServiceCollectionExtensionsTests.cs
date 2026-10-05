using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Core.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class ConfigurationServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddRevelaConfigSections_CalledTwice_RegistersEachValidatorOnce()
    {
        var services = new ServiceCollection();

        services.AddRevelaConfigSections();
        services.AddRevelaConfigSections();

        var validators = services
            .Where(d => d.ServiceType == typeof(IValidateOptions<ProjectConfig>)
                || d.ServiceType == typeof(IValidateOptions<GenerateConfig>))
            .Select(d => d.ImplementationType)
            .ToList();
        Assert.HasCount(3, validators);
        Assert.HasCount(3, validators.Distinct());
    }
}
