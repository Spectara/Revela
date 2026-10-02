using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Abstractions;

/// <summary>
/// Provides pipeline step ordering information collected during command registration.
/// </summary>
/// <remarks>
/// <para>
/// This is the single source of truth for step ordering. Order values come from
/// <see cref="CommandDescriptor.Order"/> when <c>IsSequentialStep: true</c>.
/// </para>
/// <para>
/// The host populates this during command registration. Both the CLI "all" command
/// and <see cref="Sdk.Abstractions.Engine.IRevelaEngine"/> use it for sorting.
/// </para>
/// </remarks>
public interface IPipelineStepOrderProvider
{
    /// <summary>
    /// Gets the execution order for a pipeline step.
    /// </summary>
    /// <param name="category">The pipeline category (e.g., "generate", "clean").</param>
    /// <param name="name">The step name (e.g., "scan", "pages").</param>
    /// <returns>The order value, or <see cref="int.MaxValue"/> if not registered.</returns>
    int GetOrder(string category, string name);
}
