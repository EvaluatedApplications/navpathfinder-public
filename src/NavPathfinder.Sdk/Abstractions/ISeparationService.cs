using System.Collections.Immutable;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// Resolves spatial overlap between agents by iteratively repositioning them to
/// maintain a configurable minimum personal-space distance.
/// </summary>
public interface ISeparationService
{
    ValueTask<ImmutableArray<Vector2>> SeparateAsync(
        IReadOnlyList<AgentDto> agents,
        SeparationOptions? options,
        CancellationToken ct);
}
