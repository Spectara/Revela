using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Custom help action that displays subcommands grouped by category.
/// </summary>
/// <remarks>
/// Uses <see cref="CommandGroupRegistry"/> and <see cref="CommandOrderRegistry"/>
/// to organize and sort commands in the help output, matching the interactive menu layout.
/// </remarks>
internal sealed class GroupedHelpAction : SynchronousCommandLineAction
{
    private readonly CommandGroupRegistry groupRegistry;
    private readonly CommandOrderRegistry orderRegistry;
    private readonly HelpAction defaultHelp;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupedHelpAction"/> class.
    /// </summary>
    /// <param name="groupRegistry">The group registry for group ordering.</param>
    /// <param name="orderRegistry">The order registry for command sorting.</param>
    /// <param name="defaultHelp">The default help action to wrap.</param>
    public GroupedHelpAction(
        CommandGroupRegistry groupRegistry,
        CommandOrderRegistry orderRegistry,
        HelpAction defaultHelp)
    {
        this.groupRegistry = groupRegistry;
        this.orderRegistry = orderRegistry;
        this.defaultHelp = defaultHelp;
    }

    /// <inheritdoc />
    public override bool ClearsParseErrors => true;

    /// <inheritdoc />
    public override int Invoke(ParseResult parseResult)
    {
        var output = parseResult.InvocationConfiguration.Output;
        var command = parseResult.CommandResult.Command;

        // Get subcommands for grouping
        var subcommands = command.Subcommands
            .Where(c => !c.Hidden)
            .ToList();

        // Check if we have groups defined
        var grouped = orderRegistry.GetGroupedCommands(subcommands, groupRegistry);
        var hasGroups = grouped.Any(g => g.GroupName is not null);

        if (!hasGroups)
        {
            // Fall back to default help when no groups defined
            return defaultHelp.Invoke(parseResult);
        }

        // Write custom grouped help
        WriteGroupedHelp(output, command, grouped);

        return 0;
    }

    private static void WriteGroupedHelp(
        TextWriter output,
        Command command,
        IReadOnlyList<(string? GroupName, IReadOnlyList<Command> Commands)> grouped)
    {
        // Description
        if (!string.IsNullOrEmpty(command.Description))
        {
            output.WriteLine("Description:");
            output.WriteLine($"  {command.Description}");
            output.WriteLine();
        }

        // Usage - build command path from parent chain
        output.WriteLine("Usage:");
        output.WriteLine($"  {GetCommandPath(command)} [command] [options]");
        output.WriteLine();

        // Options
        output.WriteLine("Options:");
        output.WriteLine("  -?, -h, --help  Show help and usage information");
        if (command is RootCommand)
        {
            output.WriteLine("  --version       Show version information");
        }

        output.WriteLine();

        // Grouped Commands
        output.WriteLine("Commands:");

        // Calculate max command name length for alignment
        var allCommands = grouped.SelectMany(g => g.Commands).ToList();
        var maxNameLength = allCommands.Max(c => GetCommandDisplayName(c).Length);

        foreach (var (groupName, commands) in grouped)
        {
            if (commands.Count == 0)
            {
                continue;
            }

            // Write group header
            var header = groupName ?? "Other";
            output.WriteLine($"  {header}");

            // Write commands in this group
            foreach (var cmd in commands)
            {
                var displayName = GetCommandDisplayName(cmd);
                var description = cmd.Description ?? string.Empty;
                var padding = new string(' ', maxNameLength - displayName.Length + 2);
                output.WriteLine($"    {displayName}{padding}{description}");
            }
        }
    }

    private static string GetCommandDisplayName(Command cmd)
    {
        // Add arrow suffix for commands with subcommands
        var hasSubcommands = cmd.Subcommands.Any(c => !c.Hidden);
        return hasSubcommands ? $"{cmd.Name} →" : cmd.Name;
    }

    private static string GetCommandPath(Command command)
    {
        // Build full command path (e.g., "revela init" or "revela")
        var parts = new List<string>();
        var current = command;

        while (current is not null)
        {
            if (current is RootCommand)
            {
                parts.Add(GetExecutableName());
            }
            else
            {
                parts.Add(current.Name);
            }

            current = current.Parents.OfType<Command>().FirstOrDefault();
        }

        parts.Reverse();
        return string.Join(" ", parts);
    }

    private static string GetExecutableName()
    {
        // Get the executable name without path and extension
        var name = Environment.ProcessPath;
        if (string.IsNullOrEmpty(name))
        {
            return "revela";
        }
        return Path.GetFileNameWithoutExtension(name);
    }
}
