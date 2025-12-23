using System.CommandLine;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Registry for command display order and group assignment in interactive menu.
/// </summary>
/// <remarks>
/// Commands with lower order values appear first in menus.
/// Commands with the same order are sorted alphabetically.
/// Default order is 50, giving plugins room to insert before (1-49) or after (51-100).
/// Commands can optionally be assigned to groups for visual organization.
/// </remarks>
internal sealed class CommandOrderRegistry
{
    /// <summary>
    /// Default order for commands without explicit ordering.
    /// </summary>
    public const int DefaultOrder = 50;

    private readonly Dictionary<Command, int> orderMap = [];
    private readonly Dictionary<Command, string> groupMap = [];

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
