namespace NavPathfinder.Sdk.Models;

/// <summary>
/// SDK-facing representation of a circular blocked zone.
/// Passed to <see cref="Services.DynamicObstacleService.UpdateAsync"/> to describe
/// which areas of the world are impassable in the new frame.
///
/// Walkability rule applied by the pipeline:
/// a triangle's centroid is blocked when <c>Vector2.Distance(centroid, Center) &lt;= Radius</c>
/// (on-boundary is treated as blocked).
/// </summary>
/// <param name="Center">World-space position of the zone's centre.</param>
/// <param name="Radius">Exclusion radius in world units (must be &gt; 0 to affect anything).</param>
public record NavMeshBlockedZone(Vector2 Center, float Radius);
