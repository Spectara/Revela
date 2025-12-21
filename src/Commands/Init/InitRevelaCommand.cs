using System.CommandLine;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Command to initialize the global Revela configuration.
/// </summary>
/// <remarks>
/// Creates revela.json with default settings.
/// Location depends on installation type (portable vs dotnet tool).
/// </remarks>
public sealed partial class InitRevelaCommand(
    ILogger<InitRevelaCommand> logger,
    IScaffoldingService scaffoldingService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Overwrite existing configuration"
        };

        var command = new Command("revela", "Initialize global Revela configuration");
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var force = parseResult.GetValue(forceOption);
            return await ExecuteAsync(force, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private Task<int> ExecuteAsync(bool force, CancellationToken cancellationToken)
    {
        _ = cancellationToken; // Reserved for future async operations

        var configPath = GlobalConfigManager.ConfigFilePath;
        var configDir = ConfigPathResolver.ConfigDirectory;

        AnsiConsole.MarkupLine("[cyan]Initializing Revela configuration[/]\n");

        // Check if config already exists
        if (File.Exists(configPath) && !force)
        {
            AnsiConsole.MarkupLine($"[yellow]Configuration already exists:[/] {configPath}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Use [cyan]--force[/] to overwrite, or [cyan]revela config[/] to modify.");
            return Task.FromResult(0);
        }

        // Ensure directory exists
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
            LogCreatedDirectory(logger, configDir);
        }

        // Copy template
        var templatePath = "Revela/revela.json";
        if (!scaffoldingService.TemplateExists(templatePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Template not found.");
            return Task.FromResult(1);
        }

        scaffoldingService.CopyTemplateTo(templatePath, configPath);
        LogCreatedConfig(logger, configPath);

        // Show result
        var locationType = ConfigPathResolver.IsPortableInstallation ? "portable" : "user";

        AnsiConsole.MarkupLine($"[green]✓[/] Created configuration ({locationType})");
        AnsiConsole.WriteLine();

        var panel = new Panel(new Markup($"[dim]{configPath}[/]"))
            .Header("[cyan]Configuration Location[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);
        AnsiConsole.Write(panel);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Next steps:[/]");
        AnsiConsole.MarkupLine("  • Add NuGet feeds: [cyan]revela config feed add <name> <url>[/]");
        AnsiConsole.MarkupLine("  • View config:     [cyan]revela config feed list[/]");

        return Task.FromResult(0);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created directory: {Path}")]
    private static partial void LogCreatedDirectory(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created global config: {Path}")]
    private static partial void LogCreatedConfig(ILogger logger, string path);
}
