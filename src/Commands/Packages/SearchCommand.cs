using System.CommandLine;
using System.Text.Json;

using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Packages;

/// <summary>
/// Command to search for packages in the local index.
/// </summary>
/// <remarks>
/// Searches the cached package index (cache/packages.json).
/// Run 'revela packages refresh' first to populate the index.
/// </remarks>
public sealed partial class SearchCommand(
    ILogger<SearchCommand> logger)
{
    private static readonly string IndexFilePath = Path.Combine(
        ConfigPathResolver.ConfigDirectory, "cache", "packages.json");

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var queryArg = new Argument<string?>("query")
        {
            Description = "Search query (optional, shows all if empty)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var typeOption = new Option<string?>("--type", "-t")
        {
            Description = "Filter by type (theme, plugin)"
        };

        var command = new Command("search", "Search for packages in the local index");
        command.Arguments.Add(queryArg);
        command.Options.Add(typeOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var query = parseResult.GetValue(queryArg);
            var type = parseResult.GetValue(typeOption);
            return await ExecuteAsync(query, type, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string? query, string? type, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(IndexFilePath))
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Package index not found.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] first.");
                return 1;
            }

            var json = await File.ReadAllTextAsync(IndexFilePath, cancellationToken);
            var index = JsonSerializer.Deserialize(json, PackageIndexJsonContext.Default.PackageIndex);

            if (index is null || index.Packages.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Package index is empty.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
                return 1;
            }

            // Check if index is outdated (older than 7 days)
            var age = DateTime.UtcNow - index.LastUpdated;
            if (age.TotalDays > 7)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Package index is {age.Days} days old.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
                AnsiConsole.WriteLine();
            }

            // Filter packages
            var packages = index.Packages.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                packages = packages.Where(p =>
                    p.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                // Map user-friendly type names to actual type names
                var typeFilter = type switch
                {
                    var t when t.Equals("theme", StringComparison.OrdinalIgnoreCase) => "RevelaTheme",
                    var t when t.Equals("plugin", StringComparison.OrdinalIgnoreCase) => "RevelaPlugin",
                    _ => type
                };

                packages = packages.Where(p =>
                    p.Types.Contains(typeFilter, StringComparer.OrdinalIgnoreCase));
            }

            var results = packages.ToList();

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No packages found.[/]");
                return 0;
            }

            // Display results
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Package")
                .AddColumn("Version")
                .AddColumn("Type")
                .AddColumn("Source")
                .AddColumn("Description");

            foreach (var package in results.OrderBy(p => p.Types.Count > 0 ? p.Types[0] : "").ThenBy(p => p.Id))
            {
                var primaryType = package.Types.Count > 0 ? package.Types[0] : "unknown";
                var (typeDisplay, typeColor) = primaryType switch
                {
                    "RevelaTheme" => ("theme", "blue"),
                    "RevelaPlugin" => ("plugin", "green"),
                    _ => ("unknown", "dim")
                };

                // Shorten package ID for display
                var shortId = package.Id
                    .Replace("Spectara.Revela.Theme.", "", StringComparison.Ordinal)
                    .Replace("Spectara.Revela.Plugin.", "", StringComparison.Ordinal);

                var description = package.Description.Length > 40
                    ? package.Description[..37] + "..."
                    : package.Description;

                table.AddRow(
                    $"[cyan]{shortId}[/]",
                    $"[dim]{package.Version}[/]",
                    $"[{typeColor}]{typeDisplay}[/]",
                    $"[dim]{package.Source}[/]",
                    $"[dim]{description}[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Found [cyan]{results.Count}[/] package(s)");

            LogSearchCompleted(logger, query ?? "*", results.Count);
            return 0;
        }
        catch (Exception ex)
        {
            LogSearchFailed(logger, ex);
            ErrorPanels.ShowException(ex, "Failed to search packages.");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search completed: query='{Query}', results={ResultCount}")]
    private static partial void LogSearchCompleted(ILogger logger, string query, int resultCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to search packages")]
    private static partial void LogSearchFailed(ILogger logger, Exception exception);
}
