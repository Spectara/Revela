using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// Unit tests for <see cref="NuGetSourceManager"/>
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class NuGetSourceManagerTests
{
    private string root = null!;
    private string globalDirectory = null!;
    private string projectDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        root = Directory.CreateTempSubdirectory("revela-feeds-").FullName;
        globalDirectory = Directory.CreateDirectory(Path.Combine(root, "global")).FullName;
        projectDirectory = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void DefaultSource_IsNuGetOrg()
    {
        var source = NuGetSourceManager.DefaultSource;

        Assert.AreEqual("nuget.org", source.Name);
        Assert.AreEqual("https://api.nuget.org/v3/index.json", source.Url);
        Assert.IsTrue(source.Enabled);
    }

    [TestMethod]
    public async Task LoadSourcesAsync_NoFeeds_IncludesNuGetOrgAsBuiltIn()
    {
        using var context = Create(globalJson: null, projectJson: null);

        var sources = await context.Manager.LoadSourcesAsync();
        var withLocation = await context.Manager.GetAllSourcesWithLocationAsync();

        Assert.AreEqual("https://api.nuget.org/v3/index.json", sources.Single(s => s.Name == "nuget.org").Url);
        Assert.AreEqual("built-in", withLocation.Single(s => s.Source.Name == "nuget.org").Location);
        Assert.IsEmpty(context.Manager.GetProjectFeeds());
    }

    [TestMethod]
    public async Task LoadSourcesAsync_GlobalRelativeFeed_ResolvesRelativeToRevelaJsonAndIsTrusted()
    {
        using var context = Create(
            globalJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "local": "../global-feed" } } }""",
            projectJson: /*lang=json,strict*/ """{ "project": { "name": "demo" } }""");

        var sources = await context.Manager.LoadSourcesAsync();

        var feed = sources.Single(s => s.Name == "local");
        Assert.AreEqual(Path.GetFullPath(Path.Combine(globalDirectory, "..", "global-feed")), feed.Url);
        Assert.IsFalse(feed.IsProjectFeed);
        Assert.IsEmpty(context.Manager.GetPendingProjectFeeds());
    }

    [TestMethod]
    public async Task ProjectOnlyFeed_IsExcludedUntilApprovedAndResolvesRelativeToProjectJson()
    {
        using var context = Create(
            globalJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "corp": "https://corp.example/v3/index.json" } } }""",
            projectJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "test": "./my-feed", "remote": "https://project.example/v3/index.json" } } }""");

        var before = await context.Manager.LoadSourcesAsync();
        var pending = context.Manager.GetPendingProjectFeeds();

        Assert.IsTrue(before.Any(s => s.Name == "corp"));
        Assert.IsFalse(before.Any(s => s.Name is "test" or "remote"));
        Assert.HasCount(2, pending);
        Assert.AreEqual(Path.Combine(projectDirectory, "my-feed"), pending.Single(s => s.Name == "test").Url);
        Assert.AreEqual("https://project.example/v3/index.json", pending.Single(s => s.Name == "remote").Url);
        Assert.IsTrue(pending.All(s => s.IsProjectFeed));

        context.Manager.ApproveProjectFeeds();

        var after = await context.Manager.LoadSourcesAsync();
        Assert.IsEmpty(context.Manager.GetPendingProjectFeeds());
        Assert.HasCount(2, context.Manager.GetProjectFeeds());
        Assert.AreEqual(Path.Combine(projectDirectory, "my-feed"), after.Single(s => s.Name == "test").Url);
        Assert.IsTrue(after.Any(s => s.Name == "remote"));
    }

    [TestMethod]
    public async Task ProjectFeed_AlsoDeclaredGlobally_IsTrusted()
    {
        using var context = Create(
            globalJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "shared": "https://shared.example/v3/index.json", "folder": "../project/feed" } } }""",
            projectJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "shared": "https://shared.example/v3/index.json", "folder": "./feed" } } }""");

        var sources = await context.Manager.LoadSourcesAsync();

        Assert.IsEmpty(context.Manager.GetProjectFeeds());
        Assert.AreEqual("https://shared.example/v3/index.json", sources.Single(s => s.Name == "shared").Url);
        Assert.AreEqual(Path.Combine(projectDirectory, "feed"), sources.Single(s => s.Name == "folder").Url);
    }

    [TestMethod]
    public async Task ProjectFeed_OverridingGlobalNameWithOtherLocation_RequiresConsent()
    {
        using var context = Create(
            globalJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "corp": "https://corp.example/v3/index.json" } } }""",
            projectJson: /*lang=json,strict*/ """{ "dependencies": { "feeds": { "corp": "https://evil.example/v3/index.json" } } }""");

        var sources = await context.Manager.LoadSourcesAsync();

        Assert.IsFalse(sources.Any(s => s.Name == "corp"));
        Assert.AreEqual("https://evil.example/v3/index.json", context.Manager.GetPendingProjectFeeds().Single().Url);
    }

    [TestMethod]
    [DataRow("https://api.nuget.org/v3/index.json")]
    [DataRow("http://127.0.0.1:5000/v3/index.json")]
    public void ResolveFeedLocation_Url_ReturnsUnchanged(string feed) =>
        Assert.AreEqual(feed, NuGetSourceManager.ResolveFeedLocation(feed, globalDirectory));

    [TestMethod]
    public void ResolveFeedLocation_RootedPath_ReturnsUnchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), "feed");

        Assert.AreEqual(path, NuGetSourceManager.ResolveFeedLocation(path, root));
    }

    [TestMethod]
    [DataRow("../plugins", "plugins")]
    [DataRow("./local-packages", "global/local-packages")]
    [DataRow("packages", "global/packages")]
    public void ResolveFeedLocation_RelativePath_ResolvesAgainstDeclaringDirectory(string relative, string expected) =>
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(root, expected)),
            NuGetSourceManager.ResolveFeedLocation(relative, globalDirectory));

    private FeedContext Create(string? globalJson, string? projectJson)
    {
        var globalPath = Path.Combine(globalDirectory, "revela.json");
        var projectPath = Path.Combine(projectDirectory, "project.json");
        if (globalJson is not null)
        {
            File.WriteAllText(globalPath, globalJson);
        }

        if (projectJson is not null)
        {
            File.WriteAllText(projectPath, projectJson);
        }

        var configuration = (ConfigurationRoot)new ConfigurationBuilder()
            .AddJsonFile(globalPath, optional: true, reloadOnChange: false)
            .AddJsonFile(projectPath, optional: true, reloadOnChange: false)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRevelaConfigSections();
        var provider = services.BuildServiceProvider();
        var globalConfig = Substitute.For<IGlobalConfigManager>();
        globalConfig.ConfigFilePath.Returns(globalPath);
        var manager = new NuGetSourceManager(
            NullLogger<NuGetSourceManager>.Instance,
            provider.GetRequiredService<IOptionsMonitor<DependenciesConfig>>(),
            Options.Create(new ProjectEnvironment { Path = projectDirectory }),
            globalConfig);
        return new FeedContext(manager, provider, configuration);
    }

    private sealed class FeedContext(NuGetSourceManager manager, ServiceProvider provider, ConfigurationRoot configuration) : IDisposable
    {
        public NuGetSourceManager Manager { get; } = manager;

        public void Dispose()
        {
            provider.Dispose();
            configuration.Dispose();
        }
    }
}
