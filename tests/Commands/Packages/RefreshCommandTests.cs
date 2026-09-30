using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Core.Models;

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

    /// <summary>
    /// Serves a NuGet service index at <c>serviceIndexUrl</c> and a fixed search response.
    /// </summary>
    private sealed class FeedHandler(string serviceIndexUrl) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var url = request.RequestUri!.ToString();
            var content = url == serviceIndexUrl
                ? $$"""{ "version": "3.0.0", "resources": [{ "@id": "{{SearchEndpoint}}", "@type": "SearchQueryService/3.5.0" }] }"""
                : url.StartsWith(SearchEndpoint, StringComparison.Ordinal) ? SearchResponse : null;

            return Task.FromResult(content is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
