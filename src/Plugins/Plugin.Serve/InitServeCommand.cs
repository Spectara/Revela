using System.CommandLine;
using System.Text.Json;
using Spectara.Revela.Plugin.Serve.Configuration;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Command to initialize serve plugin configuration
/// </summary>
/// <remarks>
/// Creates a default plugins/Spectara.Revela.Plugin.Serve.json config file.
/// </remarks>
public sealed partial class InitServeCommand(ILogger<InitServeCommand> logger)
{
    private const string PluginsFolderName = "plugins";
    private const string DefaultConfigFileName = $"{ServeConfig.SectionName}.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("serve", "Initialize serve plugin configuration");

        var portOption = new Option<int?>("--port", "-p")
        {
            Description = "Port number (default: 8080)"
        };

        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Enable verbose logging by default"
        };

        command.Options.Add(portOption);
        command.Options.Add(verboseOption);

        command.SetAction(parseResult =>
        {
            var port = parseResult.GetValue(portOption);
            var verbose = parseResult.GetValue(verboseOption);
            Execute(port, verbose);
            return 0;
        });

        return command;
    }

    private void Execute(int? portArg, bool verboseArg)
    {
        // Ensure plugins folder exists
        Directory.CreateDirectory(PluginsFolderName);
        var configPath = Path.Combine(PluginsFolderName, DefaultConfigFileName);

        // Check if already initialized
        if (File.Exists(configPath))
        {
            if (!AnsiConsole.Confirm($"[yellow]{configPath} already exists. Overwrite?[/]"))
            {
                AnsiConsole.MarkupLine("[dim]Configuration unchanged.[/]");
                return;
            }
        }

        // Get port interactively if not provided
        var port = portArg ?? AnsiConsole.Prompt(
            new TextPrompt<int>("Port number:")
                .DefaultValue(8080)
                .Validate(p => p is >= 1 and <= 65535
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Port must be between 1 and 65535")));

        // Build config object
        var config = new Dictionary<string, object>
        {
            [ServeConfig.SectionName] = new Dictionary<string, object>
            {
                ["Port"] = port,
                ["Verbose"] = verboseArg
            }
        };

        // Write config file
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);

        LogConfigCreated(logger, configPath);
        AnsiConsole.MarkupLine($"\n[green]✓[/] Created [cyan]{configPath}[/]");

        // Show summary
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Port", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        table.AddRow("Verbose", verboseArg ? "Yes" : "No");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Start the server with:[/] [cyan]revela serve[/]");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Serve config created at {Path}")]
    private static partial void LogConfigCreated(ILogger logger, string path);
}
