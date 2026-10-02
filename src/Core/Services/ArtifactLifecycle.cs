using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Invalidates artifacts along the registered dependency graph.
/// </summary>
/// <remarks>
/// Revela core registers its artifacts (<see cref="CoreArtifacts"/>) like any package. Plugins
/// tested without core may still depend on core artifacts: those identifiers are always known.
/// </remarks>
internal sealed class ArtifactLifecycle(
    IEnumerable<IArtifactInvalidator> artifactInvalidators) : IArtifactLifecycle
{
    private readonly IReadOnlyList<IArtifactInvalidator> invalidators = [.. artifactInvalidators];

    public ValueTask<OperationResult> PrepareToReplaceAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default)
    {
        if (ValidateGraph() is { } validationError)
        {
            return ValueTask.FromResult(OperationResult.Fail(validationError));
        }

        if (!IsKnown(artifact))
        {
            return ValueTask.FromResult(OperationResult.Fail($"Unknown artifact '{artifact}'."));
        }

        var ordered = new List<IArtifactInvalidator>();
        var visited = new HashSet<ArtifactId> { artifact };
        CollectDependents(artifact, visited, ordered);
        return RunAsync(ordered, cancellationToken);
    }

    public ValueTask<OperationResult> InvalidateAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default)
    {
        if (ValidateGraph() is { } validationError)
        {
            return ValueTask.FromResult(OperationResult.Fail(validationError));
        }

        var target = invalidators.FirstOrDefault(item => item.Artifact == artifact);
        if (target is null)
        {
            return ValueTask.FromResult(OperationResult.Fail($"Unknown artifact '{artifact}'."));
        }

        var ordered = new List<IArtifactInvalidator>();
        Collect(target, [], ordered);
        return RunAsync(ordered, cancellationToken);
    }

    public ValueTask<OperationResult> InvalidateAllAsync(
        IReadOnlyCollection<ArtifactKind> kinds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        if (kinds.Contains(ArtifactKind.Durable))
        {
            throw new ArgumentException(
                "Durable artifacts are only removed by their owner's own clean command.",
                nameof(kinds));
        }

        if (ValidateGraph() is { } validationError)
        {
            return ValueTask.FromResult(OperationResult.Fail(validationError));
        }

        var ordered = new List<IArtifactInvalidator>();
        var visited = new HashSet<ArtifactId>();
        foreach (var target in invalidators
                     .Where(item => kinds.Contains(item.Kind))
                     .OrderBy(item => item.Artifact.Value, StringComparer.Ordinal))
        {
            Collect(target, visited, ordered);
        }

        return RunAsync(ordered, cancellationToken);
    }

    private static async ValueTask<OperationResult> RunAsync(
        IReadOnlyList<IArtifactInvalidator> ordered,
        CancellationToken cancellationToken)
    {
        foreach (var invalidator in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OperationResult result;
            try
            {
                result = await invalidator.InvalidateAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return OperationResult.Fail(
                    $"Failed to invalidate '{invalidator.Artifact}': {exception.Message}");
            }

            if (!result.Success)
            {
                return OperationResult.Fail(
                    $"Failed to invalidate '{invalidator.Artifact}': " +
                    (result.ErrorMessage ?? "Unknown error"));
            }
        }

        return OperationResult.Ok();
    }

    /// <summary>Adds the target after all of its transitive dependents.</summary>
    private void Collect(
        IArtifactInvalidator target,
        HashSet<ArtifactId> visited,
        List<IArtifactInvalidator> ordered)
    {
        if (!visited.Add(target.Artifact))
        {
            return;
        }

        CollectDependents(target.Artifact, visited, ordered);
        ordered.Add(target);
    }

    private void CollectDependents(
        ArtifactId artifact,
        HashSet<ArtifactId> visited,
        List<IArtifactInvalidator> ordered)
    {
        foreach (var dependent in invalidators
                     .Where(item => item.DependsOn.Contains(artifact))
                     .OrderBy(item => item.Artifact.Value, StringComparer.Ordinal))
        {
            Collect(dependent, visited, ordered);
        }
    }

    private bool IsKnown(ArtifactId artifact) =>
        CoreArtifacts.All.Contains(artifact) || invalidators.Any(item => item.Artifact == artifact);

    private string? ValidateGraph()
    {
        var invalidatorByArtifact = new Dictionary<ArtifactId, IArtifactInvalidator>();
        foreach (var invalidator in invalidators.OrderBy(
                     item => item.Artifact.Value,
                     StringComparer.Ordinal))
        {
            if (invalidator.Artifact.IsEmpty)
            {
                return "An artifact invalidator declared an empty artifact identifier.";
            }

            if (!Enum.IsDefined(invalidator.Kind))
            {
                return $"Artifact '{invalidator.Artifact}' declares an unknown kind.";
            }

            if (!invalidatorByArtifact.TryAdd(invalidator.Artifact, invalidator))
            {
                return $"Multiple invalidators own artifact '{invalidator.Artifact}'.";
            }
        }

        foreach (var invalidator in invalidators.OrderBy(
                     item => item.Artifact.Value,
                     StringComparer.Ordinal))
        {
            foreach (var dependency in invalidator.DependsOn.OrderBy(
                         item => item.Value,
                         StringComparer.Ordinal))
            {
                if (dependency.IsEmpty)
                {
                    return $"Artifact '{invalidator.Artifact}' declares an empty dependency.";
                }

                invalidatorByArtifact.TryGetValue(dependency, out var dependencyOwner);
                if (dependencyOwner is null && !CoreArtifacts.All.Contains(dependency))
                {
                    return $"Artifact '{invalidator.Artifact}' depends on unknown artifact '{dependency}'.";
                }

                // Core artifacts are never durable, whether or not core is registered.
                if (invalidator.Kind is ArtifactKind.Durable && dependencyOwner?.Kind is not ArtifactKind.Durable)
                {
                    return $"Durable artifact '{invalidator.Artifact}' may only depend on durable artifacts, not '{dependency}'.";
                }
            }
        }

        var visiting = new HashSet<ArtifactId>();
        var visited = new HashSet<ArtifactId>();
        foreach (var invalidator in invalidators.OrderBy(
                 item => item.Artifact.Value,
                 StringComparer.Ordinal))
        {
            if (HasCycle(invalidator.Artifact, invalidatorByArtifact, visiting, visited))
            {
                return $"Artifact dependency cycle detected at '{invalidator.Artifact}'.";
            }
        }

        return null;
    }

    private static bool HasCycle(
        ArtifactId artifact,
        IReadOnlyDictionary<ArtifactId, IArtifactInvalidator> invalidatorByArtifact,
        HashSet<ArtifactId> visiting,
        HashSet<ArtifactId> visited)
    {
        if (visited.Contains(artifact))
        {
            return false;
        }

        if (!visiting.Add(artifact))
        {
            return true;
        }

        if (invalidatorByArtifact.TryGetValue(artifact, out var invalidator))
        {
            foreach (var dependency in invalidator.DependsOn)
            {
                if (invalidatorByArtifact.ContainsKey(dependency) &&
                    HasCycle(dependency, invalidatorByArtifact, visiting, visited))
                {
                    return true;
                }
            }
        }

        visiting.Remove(artifact);
        visited.Add(artifact);
        return false;
    }
}
