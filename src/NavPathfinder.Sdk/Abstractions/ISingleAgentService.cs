using System.Numerics;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// Exposes the point-to-point path query API for agents with distinct goals.
/// Obtain a concrete instance via <see cref="PathfindingWorld.CreateSingleAgentService"/>.
/// </summary>
public interface ISingleAgentService
{
    /// <summary>
    /// Finds the optimal smoothed path for a single (start, goal) pair.
    ///
    /// Returns an empty list when start or goal is outside the navmesh, or no path exists.
    /// </summary>
    Task<IReadOnlyList<Vector2>> QueryPathAsync(
        NavMeshHandle     navMesh,
        Vector2           start,
        Vector2           goal,
        CancellationToken ct = default);

    /// <summary>
    /// Finds optimal smoothed paths for a batch of (start, goal) pairs in parallel.
    ///
    /// Returns one entry per input pair, in the same order. Empty inner lists
    /// indicate unreachable or off-navmesh queries.
    /// </summary>
    Task<IReadOnlyList<IReadOnlyList<Vector2>>> QueryPathBatchAsync(
        NavMeshHandle                              navMesh,
        IEnumerable<(Vector2 Start, Vector2 Goal)> queries,
        CancellationToken                          ct = default);
}
