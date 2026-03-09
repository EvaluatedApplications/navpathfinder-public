using Microsoft.Extensions.DependencyInjection;
using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Services;

namespace NavPathfinder.Sdk.Extensions;

/// <summary>
/// Extension methods for registering NavPathfinder SDK services with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers NavPathfinder SDK services with the dependency injection container.
    ///
    /// Registered singletons:
    /// <list type="bullet">
    ///   <item><see cref="PathfindingWorld"/> — root factory for named service instances</item>
    ///   <item><see cref="IMultiAgentService"/> — multi-agent tick service</item>
    ///   <item><see cref="INavMeshBakerService"/> — navmesh baking service</item>
    ///   <item><see cref="ISingleAgentService"/> — single-agent query service</item>
    ///   <item><see cref="IDynamicObstacleService"/> — dynamic obstacle update service</item>
    ///   <item><see cref="IInfluenceMapService"/> — influence map computation service</item>
    /// </list>
    ///
    /// For multiple named instances (e.g. separate levels), inject <see cref="PathfindingWorld"/>
    /// and call <c>CreateMultiAgentService("level-name")</c> directly.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional delegate to configure <see cref="NavPathfinderOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddNavPathfinder(
        this IServiceCollection services,
        Action<NavPathfinderOptions>? configure = null)
    {
        var options = new NavPathfinderOptions();
        configure?.Invoke(options);

        var world = new PathfindingWorld(options.LicenseKey, options.TunerStatePath);

        services.AddSingleton(world);
        services.AddSingleton<IMultiAgentService>(_ => world.CreateMultiAgentService());
        services.AddSingleton<INavMeshBakerService>(_ => world.CreateNavMeshBakerService());
        services.AddSingleton<ISingleAgentService>(_ => world.CreateSingleAgentService());
        services.AddSingleton<IDynamicObstacleService>(_ => world.CreateDynamicObstacleService());
        services.AddSingleton<IInfluenceMapService>(_ => world.CreateInfluenceMapService());

        return services;
    }
}
