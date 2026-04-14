using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Mutable implementation of <see cref="IPipelineStepOrderProvider"/>.
/// Populated during command registration, read by <c>IRevelaEngine</c>.
/// </summary>
internal sealed class PipelineStepOrderProvider : IPipelineStepOrderProvider
{
    private readonly Dictionary<(string Category, string Name), int> orders = new(StringTupleCategoryNameComparer.Instance);

    /// <summary>
    /// Registers the order for a pipeline step.
    /// </summary>
    /// <param name="category">Parent command name (e.g., "generate", "clean").</param>
    /// <param name="name">Command name (e.g., "scan", "pages").</param>
    /// <param name="order">The order value from <see cref="CommandDescriptor.Order"/>.</param>
    public void Register(string category, string name, int order) =>
        orders[(category, name)] = order;

    /// <inheritdoc />
    public int GetOrder(string category, string name) =>
        orders.TryGetValue((category, name), out var order) ? order : int.MaxValue;

    /// <summary>
    /// Equality comparer for (string, string) tuples using ordinal comparison.
    /// </summary>
    private sealed class StringTupleCategoryNameComparer : IEqualityComparer<(string Category, string Name)>
    {
        public static readonly StringTupleCategoryNameComparer Instance = new();

        public bool Equals((string Category, string Name) x, (string Category, string Name) y) =>
            string.Equals(x.Category, y.Category, StringComparison.Ordinal) &&
            string.Equals(x.Name, y.Name, StringComparison.Ordinal);

        public int GetHashCode((string Category, string Name) obj) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.Category),
                StringComparer.Ordinal.GetHashCode(obj.Name));
    }
}
