using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Generate;
using Spectara.Revela.Commands.Generate.Abstractions;

namespace Spectara.Revela.Commands.Tests;

[TestClass]
public sealed class ServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddGenerateFeature_RegistersTransientTemplateEngine()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Commands.Generate.ServiceCollectionExtensions.AddGenerateFeature(services);

        var descriptor = services.First(d => d.ServiceType == typeof(ITemplateEngine));
        Assert.AreEqual(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [TestMethod]
    public void AddGenerateFeature_RegistersTransientRenderService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGenerateFeature();

        var descriptor = services.First(d => d.ServiceType == typeof(IRenderService));
        Assert.AreEqual(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [TestMethod]
    public void AddGenerateFeature_RegistersTemplateEngineFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGenerateFeature();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<Func<ITemplateEngine>>();
        var engine = factory.Invoke();
        Assert.IsNotNull(engine);
    }
}
