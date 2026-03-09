using System.Numerics;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// Exposes the NavMesh baking API for converting level geometry into a
/// <see cref="NavMeshHandle"/> ready for pathfinding queries.
/// Obtain a concrete instance via <see cref="PathfindingWorld.CreateNavMeshBakerService"/>.
/// </summary>
public interface INavMeshBakerService
{
    /// <summary>
    /// Bakes a <see cref="NavMeshHandle"/> from raw level geometry.
    /// </summary>
    /// <param name="vertices">Non-empty list of vertex world-space positions.</param>
    /// <param name="triangles">Non-empty list of index triples referencing <paramref name="vertices"/>.</param>
    /// <param name="blockedZones">
    ///   Optional list of obstacle polygon outlines. Each polygon must contain at least 3 vertices.
    ///   Triangles whose centroids fall inside any zone are excluded from the NavMesh.
    ///   Pass null or empty to include all triangles.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="NavMeshHandle"/> wrapping the baked NavMesh.</returns>
    Task<NavMeshHandle> BakeAsync(
        IReadOnlyList<Vector2>                   vertices,
        IReadOnlyList<(int A, int B, int C)>     triangles,
        IReadOnlyList<IReadOnlyList<Vector2>>?   blockedZones = null,
        CancellationToken                        ct           = default);
}
