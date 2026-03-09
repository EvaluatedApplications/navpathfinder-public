using EvalApp.Fluent;
using NavPathfinder.Sdk.Abstractions;

namespace NavPathfinder.Sdk.Integration;

public static class NavPathfinderEvalExtensions
{
    public static IAppBuilder WithNavPathfinder(this IAppBuilder builder, PathfindingWorld world)
        => builder.WithContext(new NavPathfinderContext(world));

    public static IAppBuilder WithSeparation(this IAppBuilder builder, PathfindingWorld world, ISeparationService service)
        => builder.WithContext(new NavPathfinderContext(world, separationService: service));

    public static IAppBuilder WithCombat(this IAppBuilder builder, PathfindingWorld world, ICombatService service)
        => builder.WithContext(new NavPathfinderContext(world, combatService: service));

    public static IAppBuilder WithAdaptive(this IAppBuilder builder, PathfindingWorld world, IAdaptivePathfindingService service)
        => builder.WithContext(new NavPathfinderContext(world, adaptiveService: service));

    public static IAppBuilder WithSimulation(this IAppBuilder builder, PathfindingWorld world, ISimulationService service)
        => builder.WithContext(new NavPathfinderContext(world, simulationService: service));
}
