using EvalApp.Licensing;
using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Services;

namespace NavPathfinder.Sdk;

/// <summary>
/// The top-level entry point for the NavPathfinder SDK.
///
/// Owns a root tuner-state directory and vends service instances,
/// each isolated to a sub-directory keyed by <paramref name="serviceKey"/>.
///
/// Typical usage (unlicensed — sequential, free):
/// <code>
///   var world   = new PathfindingWorld();
///   var service = world.CreateMultiAgentService("level-1");
///   var results = await service.TickAsync(agents, navMesh, tick);
/// </code>
///
/// Licensed — full adaptive-parallel engine:
/// <code>
///   var world   = new PathfindingWorld(licenseKey: "YOUR-LICENSE-KEY");
///   var service = world.CreateMultiAgentService("level-1");
///   var results = await service.TickAsync(agents, navMesh, tick);
/// </code>
/// </summary>
public sealed class PathfindingWorld
{
    // Per-product seed — unique to NavPathfinder. Different from EvalApp.Consumer seed.
    // Captured in validation pipeline closures; not a readable field in IL.
    private static readonly LicenseGate _gate = LicenseGateFactory.ForNavPathfinder();

    /// <summary>Exposed internally so services can call CheckPeriodic on the hot path.</summary>
    internal static LicenseGate Gate => _gate;

    private readonly string  _tunerStatePath;
    private readonly string? _licenseKey;

    /// <summary>The current license mode — Unlicensed (sequential) or Licensed (full engine).</summary>
    public LicenseMode LicenseMode { get; }

    /// <summary>
    /// Initialises the world.
    /// </summary>
    /// <param name="licenseKey">
    ///   Optional license key. Omit for unlicensed sequential mode (free).
    ///   Pass a valid key for full adaptive-parallel engine (licensed).
    /// </param>
    /// <param name="tunerStatePath">
    ///   Root directory used to persist adaptive-tuner state.
    ///   Defaults to <c>{TempPath}/navpathfinder</c> when <c>null</c>.
    /// </param>
    /// <exception cref="InvalidLicenseException">
    ///   Thrown if a non-empty <paramref name="licenseKey"/> is invalid or expired.
    /// </exception>
    public PathfindingWorld(string? licenseKey = null, string? tunerStatePath = null)
    {
        LicenseMode     = _gate.Check(licenseKey);
        _licenseKey     = licenseKey;
        _tunerStatePath = tunerStatePath
            ?? Path.Combine(Path.GetTempPath(), "navpathfinder");
    }

    /// <summary>
    /// Creates a <see cref="MultiAgentService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance (e.g. "level-1", "combat-zone").
    ///   Defaults to <c>"default"</c>.
    /// </param>
    public IMultiAgentService CreateMultiAgentService(string serviceKey = "default")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new MultiAgentService(LicenseMode, _licenseKey, servicePath);
    }

    /// <summary>
    /// Creates a <see cref="NavMeshBakerService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    ///
    /// Call once at level load; pass the returned <see cref="Models.NavMeshHandle"/> to
    /// <see cref="MultiAgentService.TickAsync"/> on every game tick.
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this baker instance (e.g. "level-1-baker").
    ///   Defaults to <c>"baker"</c>.
    /// </param>
    public INavMeshBakerService CreateNavMeshBakerService(string serviceKey = "baker")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new NavMeshBakerService(LicenseMode, servicePath);
    }

    /// <summary>
    /// Creates a <see cref="SingleAgentService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    ///
    /// Use for point-to-point queries where each agent has a distinct goal.
    /// For large swarms sharing goals, prefer <see cref="CreateMultiAgentService"/>.
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance.
    ///   Defaults to <c>"single"</c>.
    /// </param>
    public ISingleAgentService CreateSingleAgentService(string serviceKey = "single")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new SingleAgentService(LicenseMode, servicePath);
    }

    /// <summary>
    /// Creates a <see cref="DynamicObstacleService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    ///
    /// Call once per scene/level; pass the resulting <see cref="Models.NavMeshHandle"/>
    /// to <see cref="MultiAgentService.TickAsync"/> after each dynamic-obstacle update.
    ///
    /// Typical per-frame usage:
    /// <code>
    ///   navMesh = await obstacleService.UpdateAsync(navMesh, currentZones);
    ///   results = await multiAgentService.TickAsync(agents, navMesh, tick);
    /// </code>
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance (e.g. "obstacles", "level-1-obstacles").
    ///   Defaults to <c>"obstacles"</c>.
    /// </param>
    public IDynamicObstacleService CreateDynamicObstacleService(string serviceKey = "obstacles")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new DynamicObstacleService(LicenseMode, servicePath);
    }

    /// <summary>
    /// Creates an <see cref="InfluenceMapService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    ///
    /// Returns one influence float per navmesh triangle — 0 = no influence, higher = stronger.
    /// Sources whose position falls outside the navmesh are silently excluded.
    ///
    /// Typical usage:
    /// <code>
    ///   var service = world.CreateInfluenceMapService();
    ///   var values  = await service.ComputeAsync(navMesh, sources);
    ///   // values[i] = combined influence at triangle i
    /// </code>
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance.
    ///   Defaults to <c>"influence"</c>.
    /// </param>
    public IInfluenceMapService CreateInfluenceMapService(string serviceKey = "influence")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new InfluenceMapService(LicenseMode, servicePath);
    }

    /// <summary>
    /// Creates a <see cref="SeparationService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance.
    ///   Defaults to <c>"separation"</c>.
    /// </param>
    public ISeparationService CreateSeparationService(string serviceKey = "separation")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new Services.SeparationService(LicenseMode, servicePath);
    }

    /// <summary>
    /// Creates a <see cref="CombatService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance.
    ///   Defaults to <c>"combat"</c>.
    /// </param>
    public ICombatService CreateCombatService(string serviceKey = "combat")
    {
        var servicePath = Path.Combine(_tunerStatePath, serviceKey);
        return new Services.CombatService(LicenseMode, servicePath);
    }

    /// <summary>
    /// Creates an <see cref="AdaptivePathfindingService"/> whose tuner state is stored at
    /// <c>{tunerStatePath}/{serviceKey}</c>.
    ///
    /// When pipeline load is below <see cref="AdaptiveOptions.GroupingThreshold"/>, delegates
    /// directly to an inner <see cref="IMultiAgentService"/> (zero overhead fast path).
    /// Above the threshold, clusters nearby agents with similar goals and runs pathfinding
    /// only for cluster representatives, then synthesises follower paths via formation offsets.
    /// </summary>
    /// <param name="serviceKey">
    ///   Logical name for this service instance.
    ///   Defaults to <c>"adaptive"</c>.
    /// </param>
    public IAdaptivePathfindingService CreateAdaptivePathfindingService(string serviceKey = "adaptive")
    {
        var servicePath  = Path.Combine(_tunerStatePath, serviceKey);
        var innerService = CreateMultiAgentService(serviceKey + "-inner");
        return new Services.AdaptivePathfindingService(LicenseMode, servicePath, innerService);
    }
}
