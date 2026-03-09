namespace NavPathfinder.Sdk.Models;

/// <summary>
/// SDK-facing influence emitter record.
/// </summary>
/// <param name="Position">World-space position of the influence emitter.</param>
/// <param name="Strength">Peak influence value at the emitter's containing triangle (distance = 0).</param>
/// <param name="FalloffRadius">World-space distance at which influence drops to exactly 0.</param>
public record InfluenceSourceDto(Vector2 Position, float Strength, float FalloffRadius);
