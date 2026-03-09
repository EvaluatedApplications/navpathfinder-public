using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// Selects the appropriate pathfinding strategy based on runtime pipeline load,
/// switching between direct per-agent pathfinding and a grouped representative
/// approach as demand rises.
/// </summary>
public interface IAdaptivePathfindingService
{
    Task<TickResult> AdaptiveTickAsync(
        IReadOnlyList<AgentDto> agents,
        NavMeshHandle           navMesh,
        int                     tickNumber,
        float                   pipelineLoad,   // 0.0-1.0 signal from TickResult.Pressure
        AdaptiveOptions?        options = null,
        CancellationToken       ct      = default);
}

/// <summary>
/// Configures the thresholds and limits that govern when and how agents are clustered
/// for population-level pathfinding.
/// </summary>
public record AdaptiveOptions(
    float  GroupingThreshold = 0.70f,  // load above which clustering kicks in
    float  GroupRadius       = 3.0f,   // max distance between agents to be in same cluster
    int    MaxGroupSize      = 8,      // max agents per cluster (extras become representatives)
    double TargetFrameMs     = 16.67); // per-population frame budget passed through to TickOptions
