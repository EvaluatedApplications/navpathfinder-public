using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// High-level black-box simulation service. Declare populations with compute weights;
/// the SDK handles all pathfinding strategy decisions internally.
///
/// Obtain via <see cref="NavSim.Create(PathfindingWorld)"/>.
/// </summary>
public interface ISimulationService
{
    /// <summary>
    /// Advances all populations by one tick. ComputeWeight in each PopulationInput
    /// is the proportional CPU share for this tick — adjustable per frame for game
    /// mechanics like morale.
    /// </summary>
    Task<SimTickResult> TickAsync(SimTickInput input, CancellationToken ct = default);
}
