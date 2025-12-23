namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Well-known group names for consistent usage.
/// </summary>
internal static class CommandGroups
{
    /// <summary>Project setup and configuration commands.</summary>
    public const string Setup = "Setup";

    /// <summary>Content creation and source management commands.</summary>
    public const string Content = "Content";

    /// <summary>Build, generation, and cleanup commands.</summary>
    public const string Build = "Build";

    /// <summary>Theme, plugin, and dependency management commands.</summary>
    public const string Addons = "Addons";
}

/// <summary>
/// Registry for command group definitions in the interactive menu.
/// </summary>
/// <remarks>
/// Groups organize commands visually in the interactive menu.
/// Each group has a display name and an order value for sorting.
/// Plugins can register new groups dynamically via <see cref="GetOrCreate"/>.
/// </remarks>
internal sealed class CommandGroupRegistry
{
    /// <summary>
    /// Default order for dynamically created groups.
    /// </summary>
    public const int DefaultOrder = 50;

    private readonly Dictionary<string, int> orderMap = new(StringComparer.OrdinalIgnoreCase);
    private int nextDynamicOrder = DefaultOrder;

    /// <summary>
    /// Registers a group with a specific display order.
    /// </summary>
    /// <param name="name">The group name (case-insensitive).</param>
    /// <param name="order">The display order (1-100, lower = first).</param>
    public void Register(string name, int order)
    {
        orderMap[name] = order;

        // Track highest order for dynamic groups
        if (order >= nextDynamicOrder)
        {
            nextDynamicOrder = order + 10;
        }
    }

    /// <summary>
    /// Gets the display order for a group.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The registered order, or <see cref="DefaultOrder"/> if not registered.</returns>
    public int GetOrder(string name) => orderMap.TryGetValue(name, out var order) ? order : DefaultOrder;

    /// <summary>
    /// Checks if a group is registered.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>True if the group exists.</returns>
    public bool Exists(string name) => orderMap.ContainsKey(name);

    /// <summary>
    /// Gets an existing group order or creates a new group with auto-incremented order.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group's display order.</returns>
    public int GetOrCreate(string name)
    {
        if (orderMap.TryGetValue(name, out var order))
        {
            return order;
        }

        // Create new group with next available order
        order = nextDynamicOrder;
        nextDynamicOrder += 10;
        orderMap[name] = order;

        return order;
    }

    /// <summary>
    /// Gets all registered group names sorted by order.
    /// </summary>
    /// <returns>Group names in display order.</returns>
    public IEnumerable<string> GetAllGroupsSorted()
    {
        return orderMap
            .OrderBy(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => kvp.Key);
    }
}
