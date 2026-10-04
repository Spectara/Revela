using System.CommandLine;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Packaging;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Features.Packages.Commands.Packages;

/// <summary>
/// Command to refresh the local package index from all feeds.
/// </summary>
/// <remarks>
/// Scans all configured feeds (bundled, nuget.org, custom) and creates
/// a local index at {ConfigDirectory}/packages.json for offline search capability.
/// </remarks>
internal sealed partial class RefreshCommand(
    ILogger<RefreshCommand> logger,
    INuGetSourceManager nugetSourceManager,
    IPackageIndexService packageIndexService,
    HttpClient httpClient,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("refresh", "Update local package index from all feeds");

        command.SetAction(async (_, cancellationToken) =>
            await RefreshAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Refreshes the local package index from all configured feeds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogRefreshingIndex(logger);

            var sources = await nugetSourceManager.GetAllSourcesWithLocationAsync(cancellationToken);
            var packages = new List<PackageIndexEntry>();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[cyan]Scanning feeds[/]", maxValue: sources.Count);

                    foreach (var (source, location) in sources)
                    {
                        task.Description = $"[cyan]Scanning[/] {Markup.Escape(source.Name)}";

                        try
                        {
                            var sourcePackages = await ScanSourceAsync(source, location, httpClient, logger, cancellationToken);
                            packages.AddRange(sourcePackages);
                            LogScannedSource(logger, source.Name, sourcePackages.Count);
                        }
                        catch (Exception ex)
                        {
                            LogScanFailed(logger, source.Name, ex);
                            // Continue with other sources
                        }

                        task.Increment(1);
                    }
                });

            // Remove duplicates (same package from multiple sources - keep first = highest priority)
            var uniquePackages = packages
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .OrderBy(p => p.Id)
                .ToList();

            // Save index
            var index = new PackageIndex
            {
                LastUpdated = timeProvider.GetUtcNow().UtcDateTime,
                Packages = uniquePackages
            };

            await packageIndexService.SaveIndexAsync(index, cancellationToken);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Indexed [cyan]{uniquePackages.Count}[/] packages from [cyan]{sources.Count}[/] sources");
            AnsiConsole.MarkupLine($"  Cache: [dim]{Markup.Escape(packageIndexService.IndexFilePath)}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            LogRefreshFailed(logger, ex);
            ErrorPanels.ShowException(ex, "Failed to refresh package index.");
            return 1;
        }
    }

    internal static async Task<List<PackageIndexEntry>> ScanSourceAsync(
        NuGetSource source,
        string location,
        HttpClient httpClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var packages = new List<PackageIndexEntry>();

        if (location is "local" or "bundled")
        {
            // Local folder - scan .nupkg files directly
            if (Directory.Exists(source.Url))
            {
                foreach (var nupkgPath in Directory.GetFiles(source.Url, "*.nupkg"))
                {
                    try
                    {
                        var archive = await ZipFile.OpenReadAsync(nupkgPath, cancellationToken);
                        await using (archive)
                        {
                            var nuspecEntry = archive.Entries.FirstOrDefault(e =>
                                e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

                            if (nuspecEntry is not null)
                            {
                                var stream = await nuspecEntry.OpenAsync(cancellationToken);
                                await using (stream)
                                {
                                    var reader = new NuspecReader(stream);

                                    // Explicitly configured local/bundled folders are trusted by prefix only
                                    var packageId = reader.GetId();
                                    if (!PackageTrustPolicy.IsOfficialPackageId(packageId))
                                    {
                                        LogIgnoredForeignPackage(logger, packageId, source.Name);
                                        continue;
                                    }

                                    // Read real PackageTypes from .nuspec
                                    var packageTypes = reader.GetPackageTypes()
                                        .Select(pt => pt.Name)
                                        .ToList();

                                    // Fallback to inference if no types defined
                                    if (packageTypes.Count == 0)
                                    {
                                        packageTypes = [.. PackageIds.InferPackageTypes(packageId)];
                                    }

                                    packages.Add(new PackageIndexEntry
                                    {
                                        Id = reader.GetId(),
                                        Version = reader.GetVersion().ToNormalizedString(),
                                        Description = reader.GetDescription() ?? "",
                                        Authors = reader.GetAuthors(),
                                        Source = source.Name,
                                        Types = packageTypes
                                    });
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid .nupkg files
                    }
                }
            }
        }
        else
        {
            if (!PackageTrustPolicy.IsAllowedSource(source.Url))
            {
                LogInsecureSourceSkipped(logger, source.Name, source.Url);
                return packages;
            }

            // Remote NuGet feed - use Search API directly via HTTP
            // This gives us access to packageTypes (SearchQueryService/3.5.0)
            // See: https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource

            // Step 1: Get service index to find search endpoint
            var serviceIndex = await httpClient.GetFromJsonAsync(
                source.Url,
                NuGetSearchJsonContext.Default.NuGetServiceIndex,
                cancellationToken);

            var searchEndpoint = serviceIndex?.Resources?
                .FirstOrDefault(r => r.Type?.StartsWith("SearchQueryService", StringComparison.Ordinal) == true)
                ?.Id;

            if (!string.IsNullOrEmpty(searchEndpoint))
            {
                // Step 2: Search for Revela packages
                var searchUrl = $"{searchEndpoint.TrimEnd('/')}?q=Spectara.Revela&skip=0&take=100&prerelease=false&semVerLevel=2.0.0";

                var response = await httpClient.GetFromJsonAsync(
                    searchUrl,
                    NuGetSearchJsonContext.Default.NuGetSearchResponse,
                    cancellationToken);

                if (response?.Data is not null)
                {
                    foreach (var result in response.Data)
                    {
                        // Full-text search matches foreign IDs too; only the reserved,
                        // nuget.org-verified Spectara.Revela.* namespace is indexed.
                        if (!PackageTrustPolicy.IsOfficialPackageId(result.Id))
                        {
                            LogIgnoredForeignPackage(logger, result.Id ?? "", source.Name);
                            continue;
                        }

                        if (!result.Verified)
                        {
                            LogIgnoredUnverifiedPackage(logger, result.Id!, source.Name);
                            continue;
                        }

                        // Extract packageTypes from response
                        var packageTypes = result.PackageTypes?
                            .Where(pt => !string.IsNullOrEmpty(pt.Name))
                            .Select(pt => pt.Name!)
                            .ToList() ?? [];

                        // Fallback to inference if no types in response
                        if (packageTypes.Count == 0)
                        {
                            packageTypes = [.. PackageIds.InferPackageTypes(result.Id ?? "")];
                        }

                        packages.Add(new PackageIndexEntry
                        {
                            Id = result.Id ?? "",
                            Version = result.Version ?? "",
                            Description = result.Description ?? "",
                            Authors = result.Authors ?? "",
                            Source = source.Name,
                            Types = packageTypes
                        });
                    }
                }
            }
        }

        return packages;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshing package index")]
    private static partial void LogRefreshingIndex(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned source {SourceName}: {PackageCount} packages")]
    private static partial void LogScannedSource(ILogger logger, string sourceName, int packageCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to scan source {SourceName}")]
    private static partial void LogScanFailed(ILogger logger, string sourceName, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to refresh package index")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring package {PackageId} from source {SourceName}: not an official Spectara.Revela.* package")]
    private static partial void LogIgnoredForeignPackage(ILogger logger, string packageId, string sourceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring package {PackageId} from source {SourceName}: remote search result is not marked as verified")]
    private static partial void LogIgnoredUnverifiedPackage(ILogger logger, string packageId, string sourceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping insecure package source {SourceName} ({SourceUrl}): use https:// or a local folder")]
    private static partial void LogInsecureSourceSkipped(ILogger logger, string sourceName, string sourceUrl);
}

// DTOs for NuGet V3 API
// See: https://learn.microsoft.com/en-us/nuget/api/overview

/// <summary>
/// NuGet V3 Service Index (index.json)
/// See: https://learn.microsoft.com/en-us/nuget/api/service-index
/// </summary>
internal sealed class NuGetServiceIndex
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("resources")]
    public List<NuGetServiceResource>? Resources { get; init; }
}

internal sealed class NuGetServiceResource
{
    [JsonPropertyName("@id")]
    public string? Id { get; init; }

    [JsonPropertyName("@type")]
    public string? Type { get; init; }
}

/// <summary>
/// NuGet Search API Response (SearchQueryService/3.5.0)
/// See: https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource
/// </summary>
internal sealed class NuGetSearchResponse
{
    [JsonPropertyName("totalHits")]
    public int TotalHits { get; init; }

    [JsonPropertyName("data")]
    public List<NuGetSearchResult>? Data { get; init; }
}

internal sealed class NuGetSearchResult
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Package authors. nuget.org returns a JSON array, other feeds may return a string.
    /// </summary>
    [JsonPropertyName("authors")]
    [JsonConverter(typeof(NuGetAuthorsConverter))]
    public string? Authors { get; init; }

    /// <summary>
    /// Whether the package ID is covered by a verified reserved prefix (nuget.org).
    /// </summary>
    [JsonPropertyName("verified")]
    public bool Verified { get; init; }

    [JsonPropertyName("packageTypes")]
    public List<NuGetPackageType>? PackageTypes { get; init; }
}

internal sealed class NuGetPackageType
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// Reads the search API <c>authors</c> field as a string or an array of strings (joined with ", ").
/// </summary>
internal sealed class NuGetAuthorsConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType is JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            throw new JsonException($"Unexpected token {reader.TokenType} for package authors.");
        }

        var authors = new List<string>();
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            if (reader.TokenType is JsonTokenType.String && reader.GetString() is { Length: > 0 } author)
            {
                authors.Add(author);
            }
            else
            {
                reader.Skip();
            }
        }

        return string.Join(", ", authors);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

[JsonSerializable(typeof(NuGetServiceIndex))]
[JsonSerializable(typeof(NuGetSearchResponse))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class NuGetSearchJsonContext : JsonSerializerContext;
