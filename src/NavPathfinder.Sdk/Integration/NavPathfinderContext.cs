using EvalApp.Context;
using NavPathfinder.Sdk.Abstractions;

namespace NavPathfinder.Sdk.Integration;

public sealed class NavPathfinderContext : GlobalContext
{
    public NavPathfinderContext(
        PathfindingWorld world,
        ISeparationService?          separationService  = null,
        ICombatService?              combatService       = null,
        IAdaptivePathfindingService? adaptiveService    = null,
        ISimulationService?          simulationService  = null)
    {
        World             = world;
        SeparationService = separationService;
        CombatService     = combatService;
        AdaptiveService   = adaptiveService;
        SimulationService = simulationService;
    }

    public PathfindingWorld             World             { get; }
    public ISeparationService?          SeparationService { get; }
    public ICombatService?              CombatService     { get; }
    public IAdaptivePathfindingService? AdaptiveService   { get; }
    public ISimulationService?          SimulationService { get; }
}
