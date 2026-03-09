namespace NavPathfinder.Sdk.Models;
// No morale fields — caller pre-scales rates before passing
/// <summary>
/// Configures the engagement radius and per-tick kill rates for a combat resolution query.
/// </summary>
public record CombatOptions(float Radius, float AttackerKillRate, float DefenderKillRate);
