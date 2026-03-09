using System.Collections.Immutable;
using EvalApp.Licensing;
using NavPathfinder.Domain;
using NavPathfinder.DynamicObstacles;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Wraps a <see cref="DynamicObstaclePipeline"/> and exposes a clean per-frame update API.
///
/// Converts SDK-facing <see cref="NavMeshBlockedZone"/> inputs into the immutable domain
/// records expected by the obstacle pipeline, runs the full four-stage update, then wraps
/// the resulting domain <c>NavMesh</c> in a new <see cref="NavMeshHandle"/>.
///
/// Construction is intentionally <c>internal</c>; callers obtain instances
/// via <see cref="PathfindingWorld.CreateDynamicObstacleService"/>.
///
/// Typical usage:
/// <code>
///   var world   = new PathfindingWorld();
///   var baker   = world.CreateNavMeshBakerService();
///   var service = world.CreateDynamicObstacleService();
///
///   var navMesh = await baker.BakeAsync(vertices, triangles);
///
///   // On each frame where obstacles move:
///   navMesh = await service.UpdateAsync(navMesh, currentBlockedZones);
///   var results = await multiAgentService.TickAsync(agents, navMesh, tick);
/// </code>
/// </summary>
public sealed class DynamicObstacleService : IDynamicObstacleService
{
    private readonly DynamicObstaclePipeline _pipeline;
    private NavMesh? _baseNavMesh;

    /// <summary>
    /// Builds the EvalApp obstacle-update pipeline once; loads any previously persisted
    /// tuner state from <paramref name="tunerStorePath"/>.
    /// </summary>
    internal DynamicObstacleService(LicenseMode licenseMode, string? tunerStorePath)
    {
        _pipeline = DynamicObstaclePipelineBuilder.Build(licenseMode, tunerStorePath);
    }

    /// <summary>
    /// Updates the <see cref="NavMeshHandle"/> to reflect a new set of blocked zones.
    ///
    /// The first call captures the provided <paramref name="current"/> as the pristine base
    /// navmesh. Subsequent calls restore previously-blocked triangles when zones no longer
    /// cover them. Call <see cref="ResetBase"/> after a full re-bake to update the base.
    ///
    /// Mapping contract:
    ///   <paramref name="current"/>       → <c>ObstacleUpdateData.Current</c>      (internal NavMesh)
    ///   <paramref name="newBlockedZones"/> → <c>ObstacleUpdateData.NewBlockedZones</c>
    ///                                        (NavMeshBlockedZone → BlockedZone)
    ///
    /// The returned handle is a brand-new <see cref="NavMeshHandle"/> wrapping an
    /// immutable <c>NavMesh</c> snapshot — safe to swap atomically between game ticks.
    /// </summary>
    /// <param name="current">
    ///   The existing NavMesh handle. Must contain at least one triangle.
    /// </param>
    /// <param name="newBlockedZones">
    ///   The complete replacement set of blocked zones (not a delta).
    ///   Pass an empty enumerable to un-block all previously blocked triangles.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="NavMeshHandle"/> wrapping the updated NavMesh.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="current"/> contains no triangles.
    /// </exception>
    public async Task<NavMeshHandle> UpdateAsync(
        NavMeshHandle                   current,
        IEnumerable<NavMeshBlockedZone> newBlockedZones,
        CancellationToken               ct = default)
    {
        // Capture the pristine baked navmesh on the first call. Passing it to the
        // pipeline enables RebuildNeighboursStep to restore triangles that leave zones.
        _baseNavMesh ??= current.Internal;

        // Map SDK zone records → domain zone records (flat value conversion, no allocation pressure)
        var blockedZones = newBlockedZones
            .Select(z => new BlockedZone(z.Center, z.Radius))
            .ToImmutableArray();

        var data = new ObstacleUpdateData(
            Current:         current.Internal,
            NewBlockedZones: blockedZones,
            BaseNavMesh:     _baseNavMesh);

        var updatedNavMesh = await _pipeline.UpdateAsync(data, ct);

        return NavMeshHandle.Wrap(updatedNavMesh);
    }

    /// <summary>
    /// Resets the captured base navmesh. Call this after a full re-bake so the next
    /// <see cref="UpdateAsync"/> call adopts the new geometry as the pristine baseline.
    /// </summary>
    public void ResetBase() => _baseNavMesh = null;
}
