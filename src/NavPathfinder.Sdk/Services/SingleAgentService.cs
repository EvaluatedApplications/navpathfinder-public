using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Licensing;
using NavPathfinder.SingleAgent;
using NavPathfinder.Sdk.Models;

using NavPathfinder.Sdk.Abstractions;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Wraps a <see cref="SingleAgentPipeline"/> and exposes a clean path-query API.
///
/// Converts SDK-facing inputs (NavMeshHandle, plain Vector2 values) into the
/// immutable domain record expected by the pipeline, runs the full three-stage
/// query, and returns raw <see cref="IReadOnlyList{Vector2}"/> results.
///
/// Construction is intentionally <c>internal</c>; callers obtain instances
/// via <see cref="PathfindingWorld.CreateSingleAgentService"/>.
/// </summary>
public sealed class SingleAgentService : ISingleAgentService
{
    private readonly SingleAgentPipeline _pipeline;

    internal SingleAgentService(LicenseMode licenseMode, string? tunerStorePath)
    {
        _pipeline = SingleAgentPipelineBuilder.Build(licenseMode, tunerStorePath);
    }

    /// <summary>
    /// Finds the optimal smoothed path for a single (start, goal) pair.
    ///
    /// Returns an empty list when:
    ///   • <paramref name="start"/> or <paramref name="goal"/> is outside the navmesh.
    ///   • No path exists between start and goal (disconnected regions).
    /// </summary>
    /// <param name="navMesh">The navmesh to query. Obtained from <see cref="NavMeshBakerService"/>.</param>
    /// <param name="start">World-space start position.</param>
    /// <param name="goal">World-space goal position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   Smoothed waypoints [start, ..., goal], or an empty list when unreachable.
    /// </returns>
    public async Task<IReadOnlyList<Vector2>> QueryPathAsync(
        NavMeshHandle     navMesh,
        Vector2           start,
        Vector2           goal,
        CancellationToken ct = default)
    {
        var results = await QueryPathBatchAsync(navMesh, [(start, goal)], ct);
        return results[0];
    }

    /// <summary>
    /// Finds optimal smoothed paths for a batch of (start, goal) pairs in parallel.
    ///
    /// Returns one entry per input pair, in the same order. Empty inner lists
    /// indicate unreachable or off-navmesh queries.
    /// </summary>
    /// <param name="navMesh">The navmesh to query.</param>
    /// <param name="queries">One or more (Start, Goal) pairs to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   One <see cref="IReadOnlyList{Vector2}"/> per input pair.
    ///   Empty inner lists = unreachable / off-navmesh.
    /// </returns>
    public async Task<IReadOnlyList<IReadOnlyList<Vector2>>> QueryPathBatchAsync(
        NavMeshHandle                             navMesh,
        IEnumerable<(Vector2 Start, Vector2 Goal)> queries,
        CancellationToken                         ct = default)
    {
        var queryArray = queries.ToImmutableArray();

        if (queryArray.IsEmpty)
            return Array.Empty<IReadOnlyList<Vector2>>();

        // Map SDK inputs → internal domain record
        var data = new SingleQueryData(
            NavMesh: navMesh.Internal,
            Queries: queryArray);

        // Execute the three-stage pipeline (ValidatePositions → RunAStar → SmoothPaths)
        var paths = await _pipeline.QueryBatchAsync(data, ct);

        // Map back: ImmutableArray<Vector2> → IReadOnlyList<Vector2>
        // (ImmutableArray already implements IReadOnlyList — cast avoids array copy)
        return paths
            .Select(p => (IReadOnlyList<Vector2>)p)
            .ToList();
    }
}
