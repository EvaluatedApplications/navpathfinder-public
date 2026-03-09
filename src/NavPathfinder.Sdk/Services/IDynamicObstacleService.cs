using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Updates a navmesh handle in response to moving blocked zones, restoring previously-blocked
/// triangles when zones no longer cover them.
/// </summary>
public interface IDynamicObstacleService
{
    /// <summary>
    /// Updates the <see cref="NavMeshHandle"/> to reflect a new set of blocked zones.
    ///
    /// The first call captures the provided <paramref name="current"/> as the pristine base
    /// navmesh. Subsequent calls restore previously-blocked triangles when zones no longer
    /// cover them. Call <see cref="ResetBase"/> after a full re-bake to update the base.
    ///
    /// The returned handle is a brand-new <see cref="NavMeshHandle"/> wrapping an
    /// immutable navmesh snapshot — safe to swap atomically between game ticks.
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
    Task<NavMeshHandle> UpdateAsync(
        NavMeshHandle                   current,
        IEnumerable<NavMeshBlockedZone> newBlockedZones,
        CancellationToken               ct = default);

    /// <summary>
    /// Resets the captured base navmesh. Call this after a full re-bake so the next
    /// <see cref="UpdateAsync"/> call adopts the new geometry as the pristine baseline.
    /// </summary>
    void ResetBase();
}
