using System.Collections.Immutable;

namespace NavPathfinder.Sdk.Models;

/// <summary>
/// Contains per-population results from a completed simulation tick.
/// </summary>
public record SimTickResult(
    ImmutableArray<PopulationTickResult> Populations
)
{
    public PopulationTickResult? GetPopulation(string name)
        => Populations.FirstOrDefault(p => p.Name == name);
}

/// <summary>
/// Holds the pathfinding results, pressure reading, and elapsed time for a single
/// population after a tick.
/// </summary>
public record PopulationTickResult(
    string Name,
    IReadOnlyList<PathResultDto> Paths,
    float Pressure,
    double ElapsedMs
);
