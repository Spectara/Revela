using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Core.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class DependenciesConfigTests
{
    [TestMethod]
    public void Bind_GlobalAndProjectLayers_MergesBothMapsPerKey()
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["dependencies:feeds:shared"] = "https://global.example/v3/index.json",
                ["dependencies:feeds:globalOnly"] = "../global-feed",
                ["dependencies:packages:Spectara.Revela.Themes.Lumina"] = "1.0.0",
                ["dependencies:packages:Acme.Revela.Watermark"] = "1.0.0"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["dependencies:feeds:SHARED"] = "https://project.example/v3/index.json",
                ["dependencies:feeds:projectOnly"] = "./feed",
                ["dependencies:packages:spectara.revela.themes.lumina"] = "2.0.0",
                ["dependencies:packages:Spectara.Revela.Plugins.Serve"] = "3.0.0"
            })
            .Build();

        var config = Bind(configuration);

        Assert.HasCount(3, config.Feeds);
        Assert.AreEqual("https://project.example/v3/index.json", config.Feeds["shared"]);
        Assert.AreEqual("../global-feed", config.Feeds["globalOnly"]);
        Assert.AreEqual("./feed", config.Feeds["projectOnly"]);
        Assert.HasCount(3, config.Packages);
        Assert.AreEqual("2.0.0", config.Packages["Spectara.Revela.Themes.Lumina"]);
        Assert.AreEqual("1.0.0", config.Packages["Acme.Revela.Watermark"]);
        Assert.AreEqual("3.0.0", config.Packages["Spectara.Revela.Plugins.Serve"]);
    }

    [TestMethod]
    public void Bind_LegacyRootMaps_AreNotDependencies()
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["themes:Spectara.Revela.Themes.Lumina"] = "1.0.0",
                ["plugins:Spectara.Revela.Plugins.Serve"] = "1.0.0",
                ["plugins:serve:port"] = "8080",
                ["packages:feeds:old"] = "https://old.example/v3/index.json"
            })
            .Build();

        var config = Bind(configuration);

        Assert.IsEmpty(config.Packages);
        Assert.IsEmpty(config.Feeds);
    }

    private static DependenciesConfig Bind(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddRevelaConfigSections();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<DependenciesConfig>>().Value;
    }
}
