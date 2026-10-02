using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// Minimal <see cref="IArtifactLifecycle"/> over a fixed set of invalidators, for plugin tests
/// that cannot see Revela core's implementation: dependents first, then the targets.
/// </summary>
/// <remarks>Graph validation is covered by the core lifecycle tests.</remarks>
public sealed class TestArtifactLifecycle(params IArtifactInvalidator[] invalidators) : IArtifactLifecycle
{
    public ValueTask<OperationResult> PrepareToReplaceAsync(ArtifactId artifact, CancellationToken cancellationToken = default) =>
        RunAsync(Dependents(artifact, []), cancellationToken);

    public ValueTask<OperationResult> InvalidateAsync(ArtifactId artifact, CancellationToken cancellationToken = default)
    {
        var target = invalidators.FirstOrDefault(item => item.Artifact == artifact);
        return target is null
            ? ValueTask.FromResult(OperationResult.Fail($"Unknown artifact '{artifact}'."))
            : RunAsync([.. Dependents(artifact, []), target], cancellationToken);
    }

    public ValueTask<OperationResult> InvalidateAllAsync(IReadOnlyCollection<ArtifactKind> kinds, CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<ArtifactId>();
        var ordered = new List<IArtifactInvalidator>();
        foreach (var target in invalidators.Where(item => kinds.Contains(item.Kind)))
        {
            ordered.AddRange(Dependents(target.Artifact, visited));
            if (visited.Add(target.Artifact))
            {
                ordered.Add(target);
            }
        }

        return RunAsync(ordered, cancellationToken);
    }

    private List<IArtifactInvalidator> Dependents(ArtifactId artifact, HashSet<ArtifactId> visited)
    {
        var ordered = new List<IArtifactInvalidator>();
        foreach (var dependent in invalidators.Where(item => item.DependsOn.Contains(artifact)))
        {
            if (visited.Add(dependent.Artifact))
            {
                ordered.AddRange(Dependents(dependent.Artifact, visited));
                ordered.Add(dependent);
            }
        }

        return ordered;
    }

    private static async ValueTask<OperationResult> RunAsync(IEnumerable<IArtifactInvalidator> ordered, CancellationToken cancellationToken)
    {
        foreach (var invalidator in ordered)
        {
            var result = await invalidator.InvalidateAsync(cancellationToken);
            if (!result.Success)
            {
                return result;
            }
        }

        return OperationResult.Ok();
    }
}
