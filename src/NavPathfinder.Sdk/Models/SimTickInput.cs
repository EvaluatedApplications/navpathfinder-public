using System.Collections.Immutable;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Models;

/// <summary>
/// Carries all inputs for a single simulation tick: the navmesh, tick counter, and
/// the populations to advance.
/// </summary>
public record SimTickInput(
    NavMeshHandle NavMesh,
    int TickNumber,
    ImmutableArray<PopulationInput> Populations
);

/// <summary>
/// Describes a named population of agents and its proportional CPU budget for a
/// given simulation tick.
/// </summary>
public record PopulationInput(
    string Name,
    IReadOnlyList<AgentDto> Agents,
    float ComputeWeight
);
