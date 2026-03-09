using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Services;

namespace NavPathfinder.Sdk;

/// <summary>
/// Entry point for the high-level simulation API.
/// </summary>
public static class NavSim
{
    public static NavSimBuilder Create(PathfindingWorld world) => new(world);
}

public sealed class NavSimBuilder
{
    private readonly PathfindingWorld _world;
    private double _frameBudgetMs = 16.67;
    private readonly List<PopulationSpec> _populations = [];

    internal NavSimBuilder(PathfindingWorld world) => _world = world;

    public NavSimBuilder WithFrameBudget(double milliseconds)
    {
        _frameBudgetMs = milliseconds;
        return this;
    }

    public NavSimBuilder AddPopulation(string name, int maxCount, float computeWeight)
    {
        _populations.Add(new(name, maxCount, computeWeight));
        return this;
    }

    /// <summary>Creates the simulation service. Call once at level load.</summary>
    public ISimulationService Build()
    {
        var services = _populations.ToDictionary(
            p => p.Name,
            p => _world.CreateAdaptivePathfindingService($"sim-{p.Name.ToLower()}"),
            StringComparer.OrdinalIgnoreCase);

        return new SimulationService(_frameBudgetMs, services, _world.LicenseMode);
    }

    private record PopulationSpec(string Name, int MaxCount, float ComputeWeight);
}
