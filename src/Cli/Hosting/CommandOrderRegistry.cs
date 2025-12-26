using System.CommandLine;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Registry for command display order, group assignment, and project requirement in interactive menu.
/// </summary>
/// <remarks>
/// Commands with lower order values appear first in menus.
/// Commands with the same order are sorted alphabetically.
/// Default order is 50, giving plugins room to insert before (1-49) or after (51-100).
/// Commands can optionally be assigned to groups for visual organization.
/// Commands can be marked as requiring a project context (default: true).
/// </remarks>
internal sealed class CommandOrderRegistry
{
    /// <summary>
    /// Default order for commands without explicit ordering.
    /// </summary>
    public const int DefaultOrder = 50;

    private readonly Dictionary<Command, int> orderMap = [];
    private readonly Dictionary<Command, string> groupMap = [];
    private readonly HashSet<Command> noProjectRequired = [];
    private readonly HashSet<Command> hideWhenProjectExists = [];

    /// <summary>
    /// Registers the display order for a command.
    /// </summary>
    /// <param name="command">The command to register.</param>
    /// <param name="order">The display order (1-100, lower = first).</param>
    public void Register(Command command, int order) => orderMap[command] = order;

    /// <summary>
    /// Registers the group assignment for a command.
    /// </summary>
    /// <param name="command">The command to assign to a group.</param>
    /// <param name="groupName">The group name (must be registered in <see cref="CommandGroupRegistry"/>).</param>
    public void RegisterGroup(Command command, string groupName) => groupMap[command] = groupName;

    /// <summary>
    /// Marks a command as not requiring a project context.
    /// </summary>
    /// <remarks>
    /// By default, all commands require a project. Call this method for commands
    /// that should be available without an active project (e.g., config project, theme install).
    /// </remarks>
    /// <param name="command">The command that doesn't require a project.</param>
    public void RegisterNoProjectRequired(Command command) => noProjectRequired.Add(command);

    /// <summary>
    /// Gets whether a command requires a project context.
    /// </summary>
    /// <param name="command">The command to check.</param>
    /// <returns>True if the command requires a project (default), false otherwise.</returns>
    public bool RequiresProject(Command command) => !noProjectRequired.Contains(command);

    /// <summary>
    /// Marks a command to be hidden when a project exists.
    /// </summary>
    /// <remarks>
    /// Used for setup commands like 'init' that are only relevant
    /// when no project is configured yet.
    /// </remarks>
    /// <param name="command">The command to hide when project exists.</param>
    public void RegisterHideWhenProjectExists(Command command) => hideWhenProjectExists.Add(command);

    /// <summary>
    /// Gets whether a command should be hidden when a project exists.
    /// </summary>
    /// <param name="command">The command to check.</param>
    /// <returns>True if the command should be hidden when project exists.</returns>
    public bool ShouldHideWhenProjectExists(Command command) => hideWhenProjectExists.Contains(command);

    /// <summary>
    /// Gets the display order for a command.
    /// </summary>
    /// <param name="command">The command to look up.</param>
    /// <returns>The registered order, or <see cref="DefaultOrder"/> if not registered.</returns>
    public int GetOrder(Command command) => orderMap.TryGetValue(command, out var order) ? order : DefaultOrder;

    /// <summary>
    /// Gets the group name for a command.
    /// </summary>
    /// <param name="command">The command to look up.</param>
    /// <returns>The group name, or null if not assigned to any group.</returns>
    public string? GetGroup(Command command) => groupMap.TryGetValue(command, out var group) ? group : null;

    /// <summary>
    /// Sorts commands by order, then alphabetically by name.
    /// </summary>
    /// <param name="commands">The commands to sort.</param>
    /// <returns>Sorted commands.</returns>
    public IEnumerable<Command> Sort(IEnumerable<Command> commands)
    {
        return commands
            .OrderBy(GetOrder)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets commands organized by groups, with ungrouped commands at the end.
    /// </summary>
    /// <param name="commands">The commands to organize.</param>
    /// <param name="groupRegistry">The group registry for group ordering.</param>
    /// <returns>
    /// A list of tuples where each tuple contains:
    /// - GroupName: The group name, or null for ungrouped commands
    /// - Commands: The sorted commands in that group
    /// </returns>
    public IReadOnlyList<(string? GroupName, IReadOnlyList<Command> Commands)> GetGroupedCommands(
        IEnumerable<Command> commands,
        CommandGroupRegistry groupRegistry)
    {
        var commandList = commands.ToList();

        // Separate grouped and ungrouped commands
        var grouped = commandList
            .Where(c => groupMap.ContainsKey(c))
            .GroupBy(c => groupMap[c])
            .OrderBy(g => groupRegistry.GetOrder(g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                GroupName: (string?)g.Key,
                Commands: (IReadOnlyList<Command>)[.. Sort(g)]))
            .ToList();

        var ungrouped = commandList
            .Where(c => !groupMap.ContainsKey(c))
            .ToList();

        if (ungrouped.Count > 0)
        {
            grouped.Add((null, [.. Sort(ungrouped)]));
        }

        return grouped;
    }
}
