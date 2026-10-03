using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Packages;
using Spectre.Console;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Unit")]
public sealed class RefreshCommandTests
{
    private const string SearchEndpoint = "https://search.test/query";

    private const string SearchResponse = /*lang=json,strict*/
        """
        {
          "totalHits": 4,
          "data": [
            { "id": "Spectara.Revela.Plugins.Verified", "version": "1.0.0", "description": "Official", "authors": "Spectara", "verified": true, "packageTypes": [{ "name": "RevelaPlugin" }] },
            { "id": "spectara.revela.plugins.lowercase", "version": "1.0.0", "description": "Official, lower-case ID", "authors": "Spectara", "verified": true, "packageTypes": [{ "name": "RevelaPlugin" }] },
            { "id": "Spectara.Revela.Plugins.Unverified", "version": "1.0.0", "description": "Unverified", "authors": "Someone", "verified": false, "packageTypes": [{ "name": "RevelaPlugin" }] },
            { "id": "Evil.Spectara.Revela.Plugins.Squat", "version": "9.9.9", "description": "Squatter", "authors": "Attacker", "verified": true, "packageTypes": [{ "name": "RevelaPlugin" }] }
          ]
        }
        """;

    private static readonly string[] ExpectedRemoteIds = ["Spectara.Revela.Plugins.Verified", "spectara.revela.plugins.lowercase"];

    [TestMethod]
    public async Task ScanSourceAsync_RemoteSearchResults_KeepsOnlyVerifiedOfficialPackages()
    {
        using var handler = new FeedHandler("https://feed.test/v3/index.json");
        using var httpClient = new HttpClient(handler);
        var source = new NuGetSource { Name = "nuget.org", Url = "https://feed.test/v3/index.json" };

        var packages = await RefreshCommand.ScanSourceAsync(source, "built-in", httpClient, NullLogger.Instance, CancellationToken.None);

        CollectionAssert.AreEquivalent(ExpectedRemoteIds, packages.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public async Task ScanSourceAsync_PlainHttpRemoteSource_IsRejectedWithoutRequest()
    {
        using var handler = new FeedHandler("http://feed.test/v3/index.json");
        using var httpClient = new HttpClient(handler);
        var source = new NuGetSource { Name = "insecure", Url = "http://feed.test/v3/index.json" };

        var packages = await RefreshCommand.ScanSourceAsync(source, "remote", httpClient, NullLogger.Instance, CancellationToken.None);

        Assert.IsEmpty(packages);
        Assert.AreEqual(0, handler.RequestCount);
    }

    [TestMethod]
    public async Task ScanSourceAsync_LoopbackHttpRemoteSource_IsScanned()
    {
        using var handler = new FeedHandler("http://localhost:5555/v3/index.json");
        using var httpClient = new HttpClient(handler);
        var source = new NuGetSource { Name = "local-server", Url = "http://localhost:5555/v3/index.json" };

        var packages = await RefreshCommand.ScanSourceAsync(source, "remote", httpClient, NullLogger.Instance, CancellationToken.None);

        Assert.Contains("Spectara.Revela.Plugins.Verified", packages.Select(p => p.Id));
    }

    [TestMethod]
    public async Task ScanSourceAsync_LocalFolder_KeepsOnlyOfficialPrefix()
    {
        var feed = Directory.CreateTempSubdirectory("revela-refresh-").FullName;
        try
        {
            _ = TestPackageFactory.CreatePackage(feed, "Spectara.Revela.Plugins.Local", "1.0.0");
            _ = TestPackageFactory.CreatePackage(feed, "Evil.Plugins.Local", "1.0.0");
            var source = new NuGetSource { Name = "bundled", Url = feed };
            using var httpClient = new HttpClient();

            var packages = await RefreshCommand.ScanSourceAsync(source, "bundled", httpClient, NullLogger.Instance, CancellationToken.None);

            Assert.HasCount(1, packages);
            Assert.AreEqual("Spectara.Revela.Plugins.Local", packages[0].Id);
        }
        finally
        {
            Directory.Delete(feed, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("Spectara.Revela.Themes.Noir", "RevelaTheme")]
    [DataRow("Spectara.Revela.Themes.Lumina.Statistics", "RevelaTheme")]
    [DataRow("Spectara.Revela.Plugins.Source.OneDrive", "RevelaPlugin")]
    public async Task ScanSourceAsync_RemoteResultWithoutPackageTypes_InfersTypeFromOfficialNamespace(string packageId, string expectedType)
    {
        var response = $$"""
            { "totalHits": 1, "data": [{ "id": "{{packageId}}", "version": "1.0.0", "description": "No types", "authors": "Spectara", "verified": true }] }
            """;
        using var handler = new FeedHandler("https://feed.test/v3/index.json", response);
        using var httpClient = new HttpClient(handler);
        var source = new NuGetSource { Name = "nuget.org", Url = "https://feed.test/v3/index.json" };

        var packages = await RefreshCommand.ScanSourceAsync(source, "built-in", httpClient, NullLogger.Instance, CancellationToken.None);

        Assert.HasCount(1, packages);
        CollectionAssert.AreEqual(new[] { expectedType }, packages[0].Types.ToArray());
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task RefreshThenSearch_LocalFeed_SearchFindsRefreshedPackage()
    {
        var root = Directory.CreateTempSubdirectory("revela-index-").FullName;
        try
        {
            var feed = Path.Combine(root, "feed");
            _ = TestPackageFactory.CreatePackage(feed, "Spectara.Revela.Plugins.Roundtrip", "1.2.3");
            var sourceManager = Substitute.For<INuGetSourceManager>();
            sourceManager.GetAllSourcesWithLocationAsync(Arg.Any<CancellationToken>())
                .Returns([(new NuGetSource { Name = "local", Url = feed }, "local")]);
            using var httpClient = new HttpClient();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(sourceManager);
            services.AddSingleton(httpClient);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IPackageIndexService>(new PackageIndexService(TimeProvider.System, Path.Combine(root, "packages.json")));
            using var provider = services.BuildServiceProvider();
            var refresh = ActivatorUtilities.CreateInstance<RefreshCommand>(provider);
            var search = ActivatorUtilities.CreateInstance<SearchCommand>(provider);

            var (refreshExit, refreshOutput) = await CaptureAsync(() => refresh.RefreshAsync());
            var (searchExit, searchOutput) = await CaptureAsync(() => search.Create().Parse(["Roundtrip"]).InvokeAsync());

            Assert.AreEqual(0, refreshExit, refreshOutput);
            Assert.AreEqual(0, searchExit, searchOutput);
            Assert.Contains("Roundtrip", searchOutput, StringComparison.Ordinal);
            Assert.Contains("1.2.3", searchOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> CaptureAsync(Func<Task<int>> action)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;
        AnsiConsole.Console = console;
        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    /// <summary>
    /// Serves a NuGet service index at <c>serviceIndexUrl</c> and a fixed search response.
    /// </summary>
    private sealed class FeedHandler(string serviceIndexUrl, string searchResponse = SearchResponse) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var url = request.RequestUri!.ToString();
            var content = url == serviceIndexUrl
                ? $$"""{ "version": "3.0.0", "resources": [{ "@id": "{{SearchEndpoint}}", "@type": "SearchQueryService/3.5.0" }] }"""
                : url.StartsWith(SearchEndpoint, StringComparison.Ordinal) ? searchResponse : null;

            return Task.FromResult(content is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
