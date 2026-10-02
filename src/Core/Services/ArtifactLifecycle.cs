using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Core.Services;

internal sealed class ArtifactLifecycle(
    IEnumerable<IArtifactInvalidator> artifactInvalidators) : IArtifactLifecycle
{
    private readonly IReadOnlyList<IArtifactInvalidator> invalidators = [.. artifactInvalidators];

    public async ValueTask<OperationResult> PrepareToReplaceAsync(
        ArtifactId artifact,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateGraph();
        if (validationError is not null)
        {
            return OperationResult.Fail(validationError);
        }

        var invalidatorByArtifact = invalidators.ToDictionary(item => item.Artifact);
        if (!CoreArtifacts.All.Contains(artifact) && !invalidatorByArtifact.ContainsKey(artifact))
        {
            return OperationResult.Fail($"Unknown artifact '{artifact}'.");
        }

        var orderedInvalidators = new List<IArtifactInvalidator>();
        var visited = new HashSet<ArtifactId>();
        CollectDependents(artifact, visited, orderedInvalidators);

        foreach (var invalidator in orderedInvalidators)
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

    private void CollectDependents(
        ArtifactId artifact,
        HashSet<ArtifactId> visited,
        List<IArtifactInvalidator> orderedInvalidators)
    {
        var dependents = invalidators
            .Where(item => item.DependsOn.Contains(artifact))
            .OrderBy(item => item.Artifact.Value, StringComparer.Ordinal);

        foreach (var dependent in dependents)
        {
            if (!visited.Add(dependent.Artifact))
            {
                continue;
            }

            CollectDependents(dependent.Artifact, visited, orderedInvalidators);
            orderedInvalidators.Add(dependent);
        }
    }

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

            if (CoreArtifacts.All.Contains(invalidator.Artifact))
            {
                return $"Artifact '{invalidator.Artifact}' is owned by Revela Core.";
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

                if (!CoreArtifacts.All.Contains(dependency) && !invalidatorByArtifact.ContainsKey(dependency))
                {
                    return $"Artifact '{invalidator.Artifact}' depends on unknown artifact '{dependency}'.";
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
