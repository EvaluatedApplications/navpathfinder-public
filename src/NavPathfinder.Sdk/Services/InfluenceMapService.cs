using System.Collections.Immutable;
using EvalApp.Licensing;
using NavPathfinder.InfluenceMap;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Wraps an <see cref="InfluenceMapPipeline"/> and exposes a clean influence-map API.
///
/// Converts SDK-facing inputs (<see cref="NavMeshHandle"/>, <see cref="InfluenceSourceDto"/>)
/// into the immutable domain records expected by the pipeline, runs the full three-stage
/// computation (BuildSourceList → PropagateInfluence [ForEach] → Merge), and returns
/// a plain <see cref="IReadOnlyList{T}"/> of floats — one per navmesh triangle.
///
/// Construction is intentionally <c>internal</c>; callers obtain instances via
/// <see cref="PathfindingWorld.CreateInfluenceMapService"/>.
///
/// Typical usage:
/// <code>
///   var world    = new PathfindingWorld();
///   var baker    = world.CreateNavMeshBakerService();
///   var service  = world.CreateInfluenceMapService();
///
///   var navMesh  = await baker.BakeAsync(vertices, triangles);
///   var values   = await service.ComputeAsync(navMesh, sources);
///   // values[i] = combined influence at triangle i
/// </code>
/// </summary>
public sealed class InfluenceMapService : IInfluenceMapService
{
    private readonly InfluenceMapPipeline _pipeline;

    /// <summary>
    /// Builds the EvalApp influence-map pipeline once; loads any previously persisted
    /// tuner state from <paramref name="tunerStorePath"/>.
    /// </summary>
    internal InfluenceMapService(LicenseMode licenseMode, string? tunerStorePath)
    {
        _pipeline = InfluenceMapPipelineBuilder.Build(licenseMode, tunerStorePath);
    }

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
    public async Task<IReadOnlyList<float>> ComputeAsync(
        NavMeshHandle                   navMesh,
        IEnumerable<InfluenceSourceDto> sources,
        CancellationToken               ct = default)
    {
        // Map SDK DTOs → domain records (flat value conversion, no allocation pressure)
        var domainSources = sources
            .Select(s => new InfluenceSource(s.Position, s.Strength, s.FalloffRadius))
            .ToImmutableArray();

        var data = new InfluenceMapData(
            NavMesh:  navMesh.Internal,
            Sources:  domainSources);

        // float[] implements IReadOnlyList<float> — cast avoids an extra copy.
        return await _pipeline.ComputeAsync(data, ct);
    }
}
