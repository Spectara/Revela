using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Serve;
using Spectara.Revela.Plugins.Serve.Configuration;
using Spectara.Revela.Plugins.Source.Calendar;
using Spectara.Revela.Plugins.Source.Calendar.Configuration;
using Spectara.Revela.Plugins.Source.OneDrive;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;
using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Plugins.Statistics.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Verifies the configuration layers for every official plugin key through the real
/// host bootstrap: property default → global <c>revela.json</c> → <c>project.json</c>
/// → <c>SPECTARA__REVELA__PLUGINS__&lt;KEY&gt;__&lt;SETTING&gt;</c>.
/// </summary>
/// <remarks>
/// These tests mutate process-wide state (environment variables and the global
/// <c>revela.json</c> next to the test binaries), so they must not run in parallel.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class PluginConfigLayerTests
{
    private const string EnvironmentPrefix = "SPECTARA__REVELA__PLUGINS__";

    [TestMethod]
    [DataRow("serve")]
    [DataRow("statistics")]
    [DataRow("oneDrive")]
    [DataRow("calendarFeeds")]
    public void ConfigureRevela_NoPluginSettings_UsesPropertyDefault(string key)
    {
        var sample = PluginSample.For(key);
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Layers" } }));

        var actual = ReadSetting(project.RootPath, sample);

        Assert.AreEqual(sample.DefaultValue, actual);
    }

    [TestMethod]
    [DataRow("serve")]
    [DataRow("statistics")]
    [DataRow("oneDrive")]
    [DataRow("calendarFeeds")]
    public void ConfigureRevela_ProjectJsonPluginSection_OverridesPropertyDefault(string key)
    {
        var sample = PluginSample.For(key);
        using var project = TestProject.Create(p => p.WithProjectJson(ProjectJson(sample, sample.ProjectValue)));

        var actual = ReadSetting(project.RootPath, sample);

        Assert.AreEqual(sample.ProjectValue, actual);
    }

    [TestMethod]
    [DataRow("serve")]
    [DataRow("statistics")]
    [DataRow("oneDrive")]
    [DataRow("calendarFeeds")]
    public void ConfigureRevela_EnvironmentVariable_OverridesProjectJson(string key)
    {
        var sample = PluginSample.For(key);
        using var project = TestProject.Create(p => p.WithProjectJson(ProjectJson(sample, sample.ProjectValue)));
        var variable = EnvironmentPrefix + key.ToUpperInvariant() + "__" + sample.EnvironmentSuffix;

        Environment.SetEnvironmentVariable(variable, sample.EnvironmentValue);
        try
        {
            var actual = ReadSetting(project.RootPath, sample);

            Assert.AreEqual(sample.EnvironmentValue, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [TestMethod]
    [DataRow("serve")]
    [DataRow("statistics")]
    [DataRow("oneDrive")]
    [DataRow("calendarFeeds")]
    public void ConfigureRevela_GlobalRevelaJson_OverridesPropertyDefault(string key)
    {
        var sample = PluginSample.For(key);
        using var project = TestProject.Create(p => p.WithProjectJson(new { project = new { name = "Layers" } }));

        var actual = WithGlobalConfig(ProjectJson(sample, sample.GlobalValue), () => ReadSetting(project.RootPath, sample));

        Assert.AreEqual(sample.GlobalValue, actual);
    }

    [TestMethod]
    [DataRow("serve")]
    [DataRow("statistics")]
    [DataRow("oneDrive")]
    [DataRow("calendarFeeds")]
    public void ConfigureRevela_ProjectJson_OverridesGlobalRevelaJson(string key)
    {
        var sample = PluginSample.For(key);
        using var project = TestProject.Create(p => p.WithProjectJson(ProjectJson(sample, sample.ProjectValue)));

        var actual = WithGlobalConfig(ProjectJson(sample, sample.GlobalValue), () => ReadSetting(project.RootPath, sample));

        Assert.AreEqual(sample.ProjectValue, actual);
    }

    private static JsonObject ProjectJson(PluginSample sample, string value) => new()
    {
        ["project"] = new JsonObject { ["name"] = "Layers" },
        ["plugins"] = new JsonObject { [sample.Key] = sample.CreateSettings(value) },
    };

    private static string ReadSetting(string projectRoot, PluginSample sample)
    {
        var builder = HostBootstrap.CreateBuilder([], new OfficialPluginSource(), projectRoot);

        using var host = builder.Build();
        return sample.Read(host.Services);
    }

    /// <summary>
    /// Writes the global <c>revela.json</c> for the duration of <paramref name="action"/>.
    /// The test host resolves it next to the test binaries (portable layout), never in the
    /// user profile; any pre-existing file is restored afterwards.
    /// </summary>
    private static string WithGlobalConfig(JsonObject content, Func<string> action)
    {
        var path = ConfigPathResolver.ConfigFilePath;
        if (!ConfigPathResolver.IsPortableInstallation ||
            !path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive($"Global config resolves outside the test output ({path}); refusing to touch the user profile.");
        }

        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        File.WriteAllText(path, content.ToJsonString());
        try
        {
            return action();
        }
        finally
        {
            if (original is null)
            {
                File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, original);
            }
        }
    }

    private sealed record PluginSample(
        string Key,
        Func<string, JsonObject> CreateSettings,
        string EnvironmentSuffix,
        string DefaultValue,
        string GlobalValue,
        string ProjectValue,
        string EnvironmentValue,
        Func<IServiceProvider, string> Read)
    {
        public static PluginSample For(string key) => key switch
        {
            "serve" => new(
                key,
                value => new JsonObject { ["port"] = int.Parse(value, CultureInfo.InvariantCulture) },
                "PORT",
                "8080",
                "5000",
                "5001",
                "5002",
                sp => sp.GetRequiredService<IOptions<ServePluginConfig>>().Value.Port.ToString(CultureInfo.InvariantCulture)),
            "statistics" => new(
                key,
                value => new JsonObject { ["maxEntriesPerCategory"] = int.Parse(value, CultureInfo.InvariantCulture) },
                "MAXENTRIESPERCATEGORY",
                "15",
                "20",
                "21",
                "22",
                sp => sp.GetRequiredService<IOptions<StatisticsPluginConfig>>().Value.MaxEntriesPerCategory.ToString(CultureInfo.InvariantCulture)),
            "oneDrive" => new(
                key,
                value => new JsonObject { ["shareUrl"] = value },
                "SHAREURL",
                string.Empty,
                "https://1drv.ms/f/global",
                "https://1drv.ms/f/project",
                "https://1drv.ms/f/environment",
                sp => sp.GetRequiredService<IOptions<OneDrivePluginConfig>>().Value.ShareUrl),
            "calendarFeeds" => new(
                key,
                value => new JsonObject
                {
                    ["feeds"] = new JsonObject
                    {
                        ["bookings"] = new JsonObject { ["url"] = value, ["output"] = "availability/bookings.ics" },
                    },
                },
                "FEEDS__BOOKINGS__URL",
                string.Empty,
                "https://example.com/global.ics",
                "https://example.com/project.ics",
                "https://example.com/environment.ics",
                sp => sp.GetRequiredService<IOptions<SourceCalendarConfig>>().Value.Feeds
                    .FirstOrDefault(feed => string.Equals(feed.Key, "bookings", StringComparison.OrdinalIgnoreCase))
                    .Value?.Url ?? string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown official plugin key."),
        };
    }

    private sealed class OfficialPluginSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() =>
        [
            new(new ServePlugin(), PackageSource.Bundled),
            new(new StatisticsPlugin(), PackageSource.Bundled),
            new(new OneDrivePlugin(), PackageSource.Bundled),
            new(new SourceCalendarPlugin(), PackageSource.Bundled),
        ];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}
