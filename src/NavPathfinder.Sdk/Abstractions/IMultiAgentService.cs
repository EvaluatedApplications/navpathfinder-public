using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// Exposes the tick-level multi-agent pathfinding API for large agent swarms sharing goals.
/// Obtain a concrete instance via <see cref="PathfindingWorld.CreateMultiAgentService"/>.
/// </summary>
public interface IMultiAgentService
{
    /// <summary>
    /// Advances pathfinding by one tick and returns one <see cref="PathResultDto"/>
    /// per input agent.
    ///
    /// Pass <paramref name="options"/> to apply scheduling limits (Explicit or Tuned mode).
    /// Pass <c>null</c> (default) for full computation with no limits (Mode.None).
    /// </summary>
    Task<TickResult> TickAsync(
        IReadOnlyList<AgentDto> agents,
        NavMeshHandle           navMesh,
        int                     tickNumber,
        TickOptions?            options = null,
        CancellationToken       ct      = default);
}
