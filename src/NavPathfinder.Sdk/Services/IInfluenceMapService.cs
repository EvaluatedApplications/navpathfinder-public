using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Computes per-triangle influence values across a navmesh from a set of positioned
/// emitters, propagating and merging each source's contribution according to
/// configurable falloff.
/// </summary>
public interface IInfluenceMapService
{
    /// <summary>
    /// Computes influence values for every navmesh triangle from the given sources.
    ///
    /// Returns one float per triangle in navmesh-index order:
    ///   • 0 = no influence (unreachable, beyond falloff, or off-mesh source excluded)
    ///   • higher values = stronger combined influence from one or more nearby sources
    ///
    /// Sources whose <see cref="InfluenceSourceDto.Position"/> falls outside the navmesh
    /// are silently excluded.  An empty <paramref name="sources"/> list returns all zeros.
    /// </summary>
    /// <param name="navMesh">The navmesh to query. Obtained from <see cref="NavMeshBakerService"/>.</param>
    /// <param name="sources">Zero or more influence emitters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   <see cref="IReadOnlyList{T}"/> of length <c>navMesh.Triangles.Count</c>;
    ///   element i = combined influence at triangle i.
    /// </returns>
    Task<IReadOnlyList<float>> ComputeAsync(
        NavMeshHandle                   navMesh,
        IEnumerable<InfluenceSourceDto> sources,
        CancellationToken               ct = default);
}
