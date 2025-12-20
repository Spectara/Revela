using System.CommandLine;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Registry for command display order in interactive menu.
/// </summary>
/// <remarks>
/// Commands with lower order values appear first in menus.
/// Commands with the same order are sorted alphabetically.
/// Default order is 50, giving plugins room to insert before (1-49) or after (51-100).
/// </remarks>
internal sealed class CommandOrderRegistry
{
    /// <summary>
    /// Default order for commands without explicit ordering.
    /// </summary>
    public const int DefaultOrder = 50;

    private readonly Dictionary<Command, int> orderMap = [];

    /// <summary>
    /// Registers the display order for a command.
    /// </summary>
    /// <param name="command">The command to register.</param>
    /// <param name="order">The display order (1-100, lower = first).</param>
    public void Register(Command command, int order)
    {
        orderMap[command] = order;
    }

    /// <summary>
    /// Gets the display order for a command.
    /// </summary>
    /// <param name="command">The command to look up.</param>
    /// <returns>The registered order, or <see cref="DefaultOrder"/> if not registered.</returns>
    public int GetOrder(Command command)
    {
        return orderMap.TryGetValue(command, out var order) ? order : DefaultOrder;
    }

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
}
