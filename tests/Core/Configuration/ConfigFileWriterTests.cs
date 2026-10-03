using System.Text;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Core.Configuration;

[TestClass]
[TestCategory("Integration")]
public sealed class ConfigFileWriterTests
{
    [TestMethod]
    public async Task WriteAsync_NewFile_WritesJsonAndReloadsConfiguration()
    {
        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "settings.json");
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(path, optional: true, reloadOnChange: false);
        var writer = CreateWriter(configuration);

        await writer.WriteAsync(path, new JsonObject { ["project"] = new JsonObject { ["name"] = "Written" } });

        Assert.AreEqual("Written", configuration["project:name"]);
        Assert.AreEqual("Written", JsonNode.Parse(await File.ReadAllTextAsync(path))?["project"]?["name"]?.GetValue<string>());
        Assert.IsEmpty(GetTempFiles(workspace.RootPath, "settings.json"));
    }

    [TestMethod]
    public async Task WriteAsync_BoundOptionsMonitor_SeesNewValueWithoutCacheInvalidation()
    {
        // Reload() fires the change token BindConfiguration registered, so IOptionsMonitor
        // re-binds by itself — no IOptionsMonitorCache.TryRemove needed by writers.
        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "project.json");
        await File.WriteAllTextAsync(path, /*lang=json,strict*/ """{ "theme": { "name": "Before" } }""");
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<ThemeConfig>().BindConfiguration(ThemeConfig.Section);
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<ThemeConfig>>();
        Assert.AreEqual("Before", monitor.CurrentValue.Name);

        await CreateWriter(configuration).WriteAsync(path, new JsonObject { ["theme"] = new JsonObject { ["name"] = "After" } });

        Assert.AreEqual("After", monitor.CurrentValue.Name);
    }

    [TestMethod]
    public async Task WriteAsync_MissingDirectory_CreatesIt()
    {
        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "nested", "config", "revela.json");

        await CreateWriter(new ConfigurationBuilder().Build()).WriteAsync(path, new JsonObject { ["key"] = "value" });

        Assert.AreEqual("value", JsonNode.Parse(await File.ReadAllTextAsync(path))?["key"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task WriteAsync_ConfigurationReaderRejectsCandidate_ThrowsAndPreservesFile()
    {
        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "site.json");
        await File.WriteAllTextAsync(path, /*lang=json,strict*/ """{ "title": "Original" }""");
        var original = await File.ReadAllBytesAsync(path);
        var candidate = new JsonObject
        {
            ["social"] = new JsonObject { ["x"] = "nested" },
            ["social:x"] = "flattened collision",
        };

        await Assert.ThrowsExactlyAsync<FormatException>(
            () => CreateWriter(new ConfigurationBuilder().Build()).WriteAsync(path, candidate));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(path));
        Assert.IsEmpty(GetTempFiles(workspace.RootPath, "site.json"));
    }

    [TestMethod]
    public async Task WriteAsync_Canceled_DoesNotWrite()
    {
        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "site.json");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateWriter(new ConfigurationBuilder().Build())
            .WriteAsync(path, new JsonObject { ["title"] = "Canceled" }, cancellationToken: cancellation.Token));

        Assert.IsFalse(File.Exists(path));
        Assert.IsEmpty(GetTempFiles(workspace.RootPath, "site.json"));
    }

    [TestMethod]
    public async Task WriteAsync_NonAsciiText_IsWrittenAsUtf8()
    {
        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "site.json");

        await CreateWriter(new ConfigurationBuilder().Build()).WriteAsync(path, new JsonObject { ["copyright"] = "© 2026 Jürgen" });

        var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();
        Assert.AreEqual("© 2026 Jürgen", configuration["copyright"]);
        Assert.IsFalse(Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)).StartsWith('\uFEFF'));
    }

    [TestMethod]
    [DataRow(UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [DataRow(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead)]
    public async Task WriteAsync_ExistingUnixFile_PreservesMode(UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "project.json");
        await File.WriteAllTextAsync(path, "{}");
        File.SetUnixFileMode(path, mode);

        await CreateWriter(new ConfigurationBuilder().Build()).WriteAsync(path, new JsonObject { ["key"] = "value" });

        Assert.AreEqual(mode, File.GetUnixFileMode(path));
    }

    [TestMethod]
    public async Task WriteAsync_NewUnixFile_IsPrivate()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "new-config.json");
        Assert.IsFalse(File.Exists(path), "The test needs a file that does not exist yet.");

        await CreateWriter(new ConfigurationBuilder().Build()).WriteAsync(path, new JsonObject { ["key"] = "value" });

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [TestMethod]
    public async Task WriteAsync_ExplicitUnixMode_OverridesExistingMode()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file permissions cannot be verified on Windows.");
            return;
        }

        using var workspace = TestProject.Create();
        var path = Path.Combine(workspace.RootPath, "revela.json");
        await File.WriteAllTextAsync(path, "{}");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

        await CreateWriter(new ConfigurationBuilder().Build()).WriteAsync(
            path,
            new JsonObject { ["key"] = "value" },
            UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    private static ConfigFileWriter CreateWriter(IConfiguration configuration) =>
        new(configuration, NullLogger<ConfigFileWriter>.Instance);

    private static string[] GetTempFiles(string directory, string fileName) =>
        Directory.GetFiles(directory, $".{fileName}.*.tmp");
}
