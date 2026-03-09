namespace NavPathfinder.Sdk.Models;
/// <summary>
/// Configures the minimum spacing, iteration count, and spatial cell size for
/// agent separation.
/// </summary>
public record SeparationOptions(float MinDistance = 1.0f, int Iterations = 2, float CellSize = 1.0f);
