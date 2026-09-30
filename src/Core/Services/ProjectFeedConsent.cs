using System.CommandLine;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Asks for consent before package feeds declared only in project.json are used.
/// </summary>
/// <remarks>
/// A project (e.g. a cloned repository) may declare its own feeds. Packages contain executable
/// code, so such feeds are never used silently: the owner confirms them interactively or passes
/// <see cref="AllowOptionName"/>. Non-interactive runs fail instead of guessing. Feeds that are
/// also declared in revela.json are trusted and never prompt.
/// </remarks>
public sealed class ProjectFeedConsent(INuGetSourceManager sourceManager, IConsoleCapabilities consoleCapabilities)
{
    /// <summary>
    /// CLI flag that approves project feeds without prompting.
    /// </summary>
    public const string AllowOptionName = "--allow-project-feeds";

    /// <summary>
    /// Creates the <see cref="AllowOptionName"/> option for install/restore commands.
    /// </summary>
    public static Option<bool> CreateOption() => new(AllowOptionName)
    {
        Description = "Use package feeds declared in project.json without asking"
    };

    /// <summary>
    /// Ensures that pending project feeds are approved before packages are resolved.
    /// </summary>
    /// <param name="allowProjectFeeds">Whether <see cref="AllowOptionName"/> was passed.</param>
    /// <param name="explicitSource">
    /// The <c>--source</c> value, if any. An explicit URL or folder bypasses configured feeds, so
    /// consent is only needed when it names a pending project feed.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when there are no pending project feeds or they were approved; otherwise <c>false</c>.</returns>
    public async Task<bool> EnsureApprovedAsync(bool allowProjectFeeds, string? explicitSource = null, CancellationToken cancellationToken = default)
    {
        var feeds = sourceManager.GetPendingProjectFeeds();
        if (explicitSource is not null)
        {
            feeds = [.. feeds.Where(feed => feed.Name.Equals(explicitSource, StringComparison.OrdinalIgnoreCase))];
        }

        if (feeds.Count == 0)
        {
            return true;
        }

        if (allowProjectFeeds)
        {
            sourceManager.ApproveProjectFeeds();
            return true;
        }

        ShowWarning(feeds);

        if (!consoleCapabilities.IsInteractive)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Project feeds need confirmation, but the console is not interactive.");
            AnsiConsole.MarkupLine($"    If you trust them, re-run with [cyan]{AllowOptionName}[/] or add them to revela.json with [cyan]revela config feed add[/].");
            return false;
        }

        if (!await AnsiConsole.ConfirmAsync("Use these project feeds for this command?", defaultValue: false, cancellationToken))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled. Project feeds were not used and nothing was installed.[/]");
            return false;
        }

        sourceManager.ApproveProjectFeeds();
        return true;
    }

    private void ShowWarning(IReadOnlyList<Models.NuGetSource> feeds)
    {
        var projectFile = sourceManager.ProjectConfigPath ?? "project.json";
        AnsiConsole.MarkupLine($"{OutputMarkers.Warning} This project declares package feeds that are not in your revela.json:");
        foreach (var feed in feeds)
        {
            AnsiConsole.MarkupLine($"    [cyan]{Markup.Escape(feed.Name)}[/]: {Markup.Escape(feed.Url)}");
        }

        AnsiConsole.MarkupLine($"    [dim]Declared in {Markup.Escape(projectFile)}. Packages from these feeds run code on this machine.[/]");
    }
}
