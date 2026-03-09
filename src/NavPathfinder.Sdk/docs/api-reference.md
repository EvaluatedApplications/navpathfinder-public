# NavPathfinder SDK — API Reference

Complete reference for all public types in `NavPathfinder.Sdk`.

---

## Table of Contents

- [Entry Points](#entry-points)
  - [PathfindingWorld](#pathfindingworld)
  - [NavSim](#navsim)
  - [NavSimBuilder](#navsimbuilder)
- [Interfaces](#interfaces)
  - [INavMeshBakerService](#inavmeshbakerservice)
  - [ISingleAgentService](#isingleagentservice)
  - [IMultiAgentService](#imultiagentservice)
  - [IAdaptivePathfindingService](#iadaptivepathfindingservice)
  - [ISeparationService](#iseparationservice)
  - [ICombatService](#icombatservice)
  - [ISimulationService](#isimulationservice)
- [Models](#models)
  - [NavMeshHandle](#navmeshhandle)
  - [NavMeshTriangleDefinition](#navmeshtriangledefinition)
  - [AgentDto](#agentdto)
  - [PathResultDto](#pathresultdto)
  - [TickResult](#tickresult)
  - [TickOptions](#tickoptions)
  - [PathfindingMode](#pathfindingmode)
  - [NavMeshBlockedZone](#navmeshblockedzone)
  - [InfluenceSourceDto](#influencesourcedto)
  - [AgentGroup](#agentgroup)
  - [AdaptiveOptions](#adaptiveoptions)
  - [SeparationOptions](#separationoptions)
  - [CombatOptions](#combatoptions)
  - [CombatResult](#combatresult)
  - [SimTickInput](#simtickinput)
  - [PopulationInput](#populationinput)
  - [SimTickResult](#simtickresult)
  - [PopulationTickResult](#populationtickresult)
- [Helpers](#helpers)
  - [AgentBudget](#agentbudget)
  - [GoalSpreading](#goalspreading)
  - [GoalSpreadingOptions](#goalspreadingoptions)
  - [SpawnPool](#spawnpool)
  - [SpawnPoolOptions](#spawnpooloptions)
- [Dependency Injection](#dependency-injection)
  - [NavPathfinderOptions](#navpathfinderoptions)
  - [ServiceCollectionExtensions](#servicecollectionextensions)

---

## Entry Points

### PathfindingWorld

The root factory for the NavPathfinder SDK. Owns a tuner-state directory and vends named
service instances. Create one instance per application.

```csharp
public sealed class PathfindingWorld
```

#### Constructor

```csharp
public PathfindingWorld(string? licenseKey = null, string? tunerStatePath = null)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `licenseKey` | `string?` | Optional license key. Omit for unlicensed sequential mode (free). Provide a valid key for the full adaptive-parallel engine. |
| `tunerStatePath` | `string?` | Root directory for persisting adaptive-tuner state. Defaults to `{TempPath}/navpathfinder`. |

**Throws** `InvalidLicenseException` if a non-null, non-empty `licenseKey` is invalid or expired.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `LicenseMode` | `LicenseMode` | Current license mode — `Unlicensed` (sequential, free) or `Licensed` (full adaptive engine). |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateMultiAgentService(string serviceKey = "default")` | `IMultiAgentService` | Creates a multi-agent tick service. Tuner state stored at `{tunerStatePath}/{serviceKey}`. |
| `CreateNavMeshBakerService(string serviceKey = "baker")` | `INavMeshBakerService` | Creates a NavMesh baker service. Call once at level load. |
| `CreateSingleAgentService(string serviceKey = "single")` | `ISingleAgentService` | Creates a single-agent path query service. |
| `CreateDynamicObstacleService(string serviceKey = "obstacles")` | `DynamicObstacleService` | Creates a dynamic obstacle update service. |
| `CreateInfluenceMapService(string serviceKey = "influence")` | `InfluenceMapService` | Creates a per-triangle influence computation service. |
| `CreateSeparationService(string serviceKey = "separation")` | `ISeparationService` | Creates an agent separation (push-apart) service. |
| `CreateCombatService(string serviceKey = "combat")` | `ICombatService` | Creates a spatial combat resolution service. |
| `CreateAdaptivePathfindingService(string serviceKey = "adaptive")` | `IAdaptivePathfindingService` | Creates an adaptive clustering tick service. Delegates to an inner `IMultiAgentService` when load is low. |

---

### NavSim

Static entry point for the high-level multi-population simulation API.

```csharp
public static class NavSim
```

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(PathfindingWorld world)` | `NavSimBuilder` | Starts a builder for a multi-population `ISimulationService`. |

---

### NavSimBuilder

Fluent builder for `ISimulationService`. Obtained via `NavSim.Create()`.

```csharp
public sealed class NavSimBuilder
```

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `WithFrameBudget(double milliseconds)` | `NavSimBuilder` | Sets the total per-tick frame budget shared across all populations. Default: `16.67` ms (60fps). |
| `AddPopulation(string name, int maxCount, float computeWeight)` | `NavSimBuilder` | Declares a named population. `computeWeight` is the proportional CPU share relative to other populations. |
| `Build()` | `ISimulationService` | Constructs the simulation service. Call once at level load. |

---

## Interfaces

### INavMeshBakerService

Converts level geometry into a `NavMeshHandle` ready for pathfinding queries. Obtain via
`PathfindingWorld.CreateNavMeshBakerService()`.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `BakeAsync` | `IReadOnlyList<Vector2> vertices`, `IReadOnlyList<(int A, int B, int C)> triangles`, `IReadOnlyList<IReadOnlyList<Vector2>>? blockedZones = null`, `CancellationToken ct = default` | `Task<NavMeshHandle>` | Bakes a navigation mesh from raw geometry. Blocked zones exclude triangles whose centroids fall inside any polygon. |

**Throws** `ArgumentException` if `vertices` or `triangles` is null or empty.

---

### ISingleAgentService

Point-to-point path query service for agents with distinct goals. Obtain via
`PathfindingWorld.CreateSingleAgentService()`.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `QueryPathAsync` | `NavMeshHandle navMesh`, `Vector2 start`, `Vector2 goal`, `CancellationToken ct = default` | `Task<IReadOnlyList<Vector2>>` | Finds the optimal smoothed path for a single (start, goal) pair. Returns empty list if start/goal is outside the mesh or no path exists. |
| `QueryPathBatchAsync` | `NavMeshHandle navMesh`, `IEnumerable<(Vector2 Start, Vector2 Goal)> queries`, `CancellationToken ct = default` | `Task<IReadOnlyList<IReadOnlyList<Vector2>>>` | Finds paths for multiple (start, goal) pairs concurrently. Returns one entry per input pair in the same order. Empty inner lists indicate unreachable or off-mesh queries. |

---

### IMultiAgentService

Tick-level multi-agent pathfinding service for large agent populations. Obtain via
`PathfindingWorld.CreateMultiAgentService()`.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `TickAsync` | `IReadOnlyList<AgentDto> agents`, `NavMeshHandle navMesh`, `int tickNumber`, `TickOptions? options = null`, `CancellationToken ct = default` | `Task<TickResult>` | Advances pathfinding by one tick. Returns one `PathResultDto` per input agent. Pass `null` options for full computation (no limits). |

**Thread safety:** Not safe to call concurrently on the same instance. Use separate instances for concurrent ticks.

---

### IAdaptivePathfindingService

Clustering-aware tick service for very large populations. Obtain via
`PathfindingWorld.CreateAdaptivePathfindingService()`.

When `pipelineLoad` is below `AdaptiveOptions.GroupingThreshold`, delegates directly to the
inner `IMultiAgentService` (zero overhead). Above the threshold, clusters nearby agents with
similar goals and computes paths only for cluster representatives.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `AdaptiveTickAsync` | `IReadOnlyList<AgentDto> agents`, `NavMeshHandle navMesh`, `int tickNumber`, `float pipelineLoad`, `AdaptiveOptions? options = null`, `CancellationToken ct = default` | `Task<TickResult>` | Advances pathfinding with optional clustering. `pipelineLoad` is typically sourced from the previous tick's `TickResult.Pressure`. |

---

### ISeparationService

Agent separation (push-apart) service for resolving physical overlaps after a tick. Obtain
via `PathfindingWorld.CreateSeparationService()`.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `SeparateAsync` | `IReadOnlyList<AgentDto> agents`, `SeparationOptions? options`, `CancellationToken ct` | `ValueTask<ImmutableArray<Vector2>>` | Returns one corrected position per input agent, in the same order. Positions are adjusted to satisfy the minimum separation distance. |

---

### ICombatService

Spatial combat resolution service. Obtain via `PathfindingWorld.CreateCombatService()`.

Given attacker and defender lists, computes which agents are killed within the engagement
radius based on kill rates. Caller is responsible for pre-scaling rates by morale or other
game mechanics before passing them in.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `ResolveAsync` | `IReadOnlyList<AgentDto> attackers`, `IReadOnlyList<AgentDto> defenders`, `CombatOptions options`, `CancellationToken ct` | `ValueTask<CombatResult>` | Resolves one combat tick. Returns IDs of killed attackers and defenders. |

---

### ISimulationService

High-level multi-population simulation service. Obtain via `NavSim.Create(world).Build()`.

Declare populations with compute weights; the SDK handles all internal scheduling and
pathfinding strategy decisions.

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `TickAsync` | `SimTickInput input`, `CancellationToken ct = default` | `Task<SimTickResult>` | Advances all populations by one tick. `ComputeWeight` in each `PopulationInput` is the proportional CPU share for this tick — adjustable per frame for game mechanics like morale. |

---

## Models

### NavMeshHandle

Opaque handle to a baked navigation mesh. Immutable after construction; thread-safe.

```csharp
public sealed class NavMeshHandle
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `TriangleCount` | `int` | Number of walkable triangles in this navigation mesh. |

#### Static Factory

```csharp
public static NavMeshHandle FromTriangles(IReadOnlyList<NavMeshTriangleDefinition> triangles)
```

Builds a `NavMeshHandle` directly from triangle definitions. Throws `ArgumentException` if
`triangles` is null or empty.

Prefer `INavMeshBakerService.BakeAsync()` for standard level geometry. Use `FromTriangles()`
when you have pre-computed triangle adjacency data.

---

### NavMeshTriangleDefinition

SDK-facing triangle definition used with `NavMeshHandle.FromTriangles()`.

```csharp
public sealed record NavMeshTriangleDefinition(
    int                Id,
    Vector2            A,
    Vector2            B,
    Vector2            C,
    IReadOnlyList<int> NeighbourIds)
```

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` | Unique triangle identifier. Must match the triangle's array index for correct mesh lookup. |
| `A` | `Vector2` | First vertex, world-space. |
| `B` | `Vector2` | Second vertex, world-space. |
| `C` | `Vector2` | Third vertex, world-space. |
| `NeighbourIds` | `IReadOnlyList<int>` | IDs of adjacent triangles that share an edge with this triangle. |

---

### AgentDto

SDK-facing snapshot of one agent for a single pathfinding tick.

```csharp
public sealed record AgentDto(
    int     Id,
    Vector2 Position,
    Vector2 Goal,
    float   Radius      = 0.5f,
    float   MaxSpeed    = 3.5f,
    bool    GoalChanged = false)
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Id` | `int` | — | Stable agent identifier. Used to correlate input agents with output `PathResultDto`. |
| `Position` | `Vector2` | — | Agent's current world-space position. |
| `Goal` | `Vector2` | — | Agent's desired destination. |
| `Radius` | `float` | `0.5` | Physical radius used for collision avoidance. |
| `MaxSpeed` | `float` | `3.5` | Maximum movement speed. |
| `GoalChanged` | `bool` | `false` | **Set `true` only when the goal changed this tick.** When `false`, the cached path is reused at zero cost. This is the most impactful performance setting. |

---

### PathResultDto

Pathfinding result for one agent after a single tick.

```csharp
public sealed record PathResultDto(
    int                    AgentId,
    IReadOnlyList<Vector2> Waypoints,
    bool                   PathFound)
```

| Field | Type | Description |
|-------|------|-------------|
| `AgentId` | `int` | Matches the `AgentDto.Id` that was passed in. |
| `Waypoints` | `IReadOnlyList<Vector2>` | Ordered world-space waypoints forming the agent's path. Empty when `PathFound` is `false`. |
| `PathFound` | `bool` | `true` when at least one waypoint was computed. `false` when `GoalChanged=false`, goal is outside the mesh, or no route exists. |

---

### TickResult

Result of a single multi-agent tick.

```csharp
public record TickResult(
    IReadOnlyList<PathResultDto> Paths,
    float                        Pressure,
    double                       ElapsedMs)
```

| Field | Type | Description |
|-------|------|-------------|
| `Paths` | `IReadOnlyList<PathResultDto>` | One result per input agent, in the same order. |
| `Pressure` | `float` | EMA-smoothed load indicator `[0, 1]`. `0.0` = idle; `1.0` = at or over budget. Always `0.0` when no `TargetFrameMs` is set. Use to drive population scaling. |
| `ElapsedMs` | `double` | Actual wall-clock tick time in milliseconds. Use for profiling. |

---

### TickOptions

Per-tick scheduling options for `IMultiAgentService.TickAsync`. Pass `null` for full
computation with no limits (default).

```csharp
public record TickOptions(
    PathfindingMode Mode                = PathfindingMode.None,
    int             MaxNewFieldsPerFrame = int.MaxValue,
    float           ConflictRadius      = float.MaxValue,
    double          TargetFrameMs       = 16.67,
    bool            SurroundingMode     = false,
    float           SurroundingRadius   = float.MaxValue,
    int             MaxDirectChasers    = 1)
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Mode` | `PathfindingMode` | `None` | Scheduling mode. See `PathfindingMode`. |
| `MaxNewFieldsPerFrame` | `int` | `int.MaxValue` | Explicit/Tuned: cap on new (uncached) guidance computations per tick. Cache hits are free and not counted. |
| `ConflictRadius` | `float` | `float.MaxValue` | Explicit/Tuned: skip collision avoidance for agents farther apart than this radius (world units). |
| `TargetFrameMs` | `double` | `16.67` | Tuned mode: desired frame budget in milliseconds. `16.67` = 60fps; `33.33` = 30fps. |
| `SurroundingMode` | `bool` | `false` | When `true`, agents sharing a goal are directed via distinct approach corridors (encirclement). Licensed only; silently ignored when unlicensed. |
| `SurroundingRadius` | `float` | `float.MaxValue` | SurroundingMode: staging applies only when agents are within this distance of each other. Use to skip staging for already-spread groups. |
| `MaxDirectChasers` | `int` | `1` | SurroundingMode: agents (closest to goal first) that bypass staging and chase directly. Rest are assigned flanking positions. |

---

### PathfindingMode

Controls how `IMultiAgentService.TickAsync` schedules work within a frame.

```csharp
public enum PathfindingMode
```

| Value | Description |
|-------|-------------|
| `None` | Full computation every frame. No scheduling limits. Pass `null` options to use this mode (default). |
| `Explicit` | Developer-controlled hard limits via `TickOptions.MaxNewFieldsPerFrame` and `ConflictRadius`. |
| `Tuned` | SDK auto-manages within `TickOptions.TargetFrameMs`. Derives limits from per-tick timing history. Recommended for production. |

---

### NavMeshBlockedZone

Circular exclusion zone for use with `DynamicObstacleService.UpdateAsync`. Triangles whose
centroids fall within `Radius` of `Center` are marked impassable.

```csharp
public record NavMeshBlockedZone(Vector2 Center, float Radius)
```

| Field | Type | Description |
|-------|------|-------------|
| `Center` | `Vector2` | World-space position of the zone's centre. |
| `Radius` | `float` | Exclusion radius in world units. Must be `> 0` to affect anything. |

---

### InfluenceSourceDto

An influence emitter for use with `InfluenceMapService.ComputeAsync`.

```csharp
public record InfluenceSourceDto(Vector2 Position, float Strength, float FalloffRadius)
```

| Field | Type | Description |
|-------|------|-------------|
| `Position` | `Vector2` | World-space position of the emitter. |
| `Strength` | `float` | Peak influence at the emitter's containing triangle (distance = 0). |
| `FalloffRadius` | `float` | World-space distance at which influence drops to exactly `0`. |

---

### AgentGroup

Result record from `IAdaptivePathfindingService` describing one cluster.

```csharp
public record AgentGroup(
    int                 RepresentativeId,
    ImmutableArray<int> FollowerIds,
    Vector2             GoalCenter)
```

| Field | Type | Description |
|-------|------|-------------|
| `RepresentativeId` | `int` | ID of the agent whose path was computed and shared to the group. |
| `FollowerIds` | `ImmutableArray<int>` | IDs of agents in this cluster that use the representative's path with formation offsets. |
| `GoalCenter` | `Vector2` | Centroid goal used for cluster formation. |

---

### AdaptiveOptions

Configuration for `IAdaptivePathfindingService.AdaptiveTickAsync`.

```csharp
public record AdaptiveOptions(
    float  GroupingThreshold = 0.70f,
    float  GroupRadius       = 3.0f,
    int    MaxGroupSize      = 8,
    double TargetFrameMs     = 16.67)
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `GroupingThreshold` | `float` | `0.70` | Pipeline load above which agent clustering activates. Below this, delegates directly to `IMultiAgentService`. |
| `GroupRadius` | `float` | `3.0` | Maximum world-space distance between agents to be grouped into the same cluster. |
| `MaxGroupSize` | `int` | `8` | Maximum agents per cluster. Agents exceeding this become additional representatives. |
| `TargetFrameMs` | `double` | `16.67` | Per-population frame budget passed through to `TickOptions`. |

---

### SeparationOptions

Configuration for `ISeparationService.SeparateAsync`.

```csharp
public record SeparationOptions(float MinDistance = 1.0f, int Iterations = 2, float CellSize = 1.0f)
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `MinDistance` | `float` | `1.0` | Minimum required distance between any two agent centres (world units). |
| `Iterations` | `int` | `2` | Number of push-apart passes. More iterations produce tighter packing but cost more. |
| `CellSize` | `float` | `1.0` | Spatial partitioning cell size for neighbourhood lookup. Match to typical `AgentDto.Radius`. |

---

### CombatOptions

Configuration for `ICombatService.ResolveAsync`.

```csharp
public record CombatOptions(float Radius, float AttackerKillRate, float DefenderKillRate)
```

| Field | Type | Description |
|-------|------|-------------|
| `Radius` | `float` | Engagement radius. Only attacker-defender pairs within this distance interact. |
| `AttackerKillRate` | `float` | Probability per combat pair per tick that the attacker kills the defender. Pre-scale by morale before passing. |
| `DefenderKillRate` | `float` | Probability per combat pair per tick that the defender kills the attacker. Pre-scale by morale before passing. |

---

### CombatResult

Result of a single `ICombatService.ResolveAsync` call.

```csharp
public record CombatResult(ImmutableArray<int> DeadAttackerIds, ImmutableArray<int> DeadDefenderIds)
```

| Field | Type | Description |
|-------|------|-------------|
| `DeadAttackerIds` | `ImmutableArray<int>` | IDs of attackers killed this tick. |
| `DeadDefenderIds` | `ImmutableArray<int>` | IDs of defenders killed this tick. |

---

### SimTickInput

Input to `ISimulationService.TickAsync`.

```csharp
public record SimTickInput(
    NavMeshHandle                   NavMesh,
    int                             TickNumber,
    ImmutableArray<PopulationInput> Populations)
```

| Field | Type | Description |
|-------|------|-------------|
| `NavMesh` | `NavMeshHandle` | The navigation mesh shared by all populations this tick. |
| `TickNumber` | `int` | Monotonically increasing tick counter. |
| `Populations` | `ImmutableArray<PopulationInput>` | One entry per declared population. |

---

### PopulationInput

Per-population input for one tick of `ISimulationService`.

```csharp
public record PopulationInput(
    string                  Name,
    IReadOnlyList<AgentDto> Agents,
    float                   ComputeWeight)
```

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Population name. Must match the name declared in `NavSimBuilder.AddPopulation`. |
| `Agents` | `IReadOnlyList<AgentDto>` | All agents in this population for this tick. |
| `ComputeWeight` | `float` | Proportional CPU share for this tick. Adjust per frame to implement morale, threat level, or priority mechanics. |

---

### SimTickResult

Result of a single `ISimulationService.TickAsync` call.

```csharp
public record SimTickResult(ImmutableArray<PopulationTickResult> Populations)
```

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetPopulation(string name)` | `PopulationTickResult?` | Returns the result for the named population, or `null` if not found. |

---

### PopulationTickResult

Per-population result within a `SimTickResult`.

```csharp
public record PopulationTickResult(
    string                       Name,
    IReadOnlyList<PathResultDto> Paths,
    float                        Pressure,
    double                       ElapsedMs)
```

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Population name. |
| `Paths` | `IReadOnlyList<PathResultDto>` | One result per agent in this population, in the same order as the input. |
| `Pressure` | `float` | Population-level load indicator `[0, 1]`. |
| `ElapsedMs` | `double` | Wall-clock time spent on this population this tick. |

---

## Helpers

Static utility classes for common game mechanics. No SDK state is required.

### AgentBudget

Calculates a target unit count based on system pressure, morale, and a population cap.

```csharp
public static class AgentBudget
```

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `Allocate` | `float pressure`, `int currentCount`, `int maxCount`, `float morale` | `int` | Returns the recommended active agent count. `pressure` is `TickResult.Pressure` (0–1). `morale` (0–1) softens the reduction: higher morale maintains more agents under pressure. Returns `0` when `currentCount <= 0`. |

**Example:**
```csharp
int target = AgentBudget.Allocate(
    pressure:     result.Pressure,
    currentCount: activeAgents.Count,
    maxCount:     MaxEnemies,
    morale:       squadMorale);
AdjustPopulationTo(target);
```

---

### GoalSpreading

Assigns agents to goal positions by nearest distance. Each goal is assigned to at most one
agent unless `AllowReuse` is set.

```csharp
public static class GoalSpreading
```

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `Spread` | `IReadOnlyList<AgentDto> agents`, `IReadOnlyList<Vector2> availableGoals`, `GoalSpreadingOptions? options = null` | `ImmutableArray<(int AgentId, Vector2 Goal)>` | Returns one (AgentId, Goal) pair per agent that could be assigned. Agents with no available goal are omitted. |

---

### GoalSpreadingOptions

```csharp
public record GoalSpreadingOptions(bool AllowReuse = false)
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `AllowReuse` | `bool` | `false` | When `true`, multiple agents may be assigned to the same goal position. |

---

### SpawnPool

Filters a list of candidate spawn points by minimum spacing, returning at most `count` positions.

```csharp
public static class SpawnPool
```

#### Methods

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `GetSpawnPoints` | `IReadOnlyList<Vector2> candidates`, `int count`, `SpawnPoolOptions? options = null` | `ImmutableArray<Vector2>` | Returns up to `count` spawn positions from `candidates`, each at least `MinSpacing` world units apart. Preserves candidate order; earlier candidates are preferred. |

---

### SpawnPoolOptions

```csharp
public record SpawnPoolOptions(float MinSpacing = 1f)
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `MinSpacing` | `float` | `1.0` | Minimum world-space distance between any two returned spawn points. |

---

## Dependency Injection

### NavPathfinderOptions

Configuration for `ServiceCollectionExtensions.AddNavPathfinder`.

```csharp
public sealed class NavPathfinderOptions
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LicenseKey` | `string?` | `null` | License key. Omit for unlicensed mode (free). Provide a valid key for the full engine. |
| `TunerStatePath` | `string?` | `null` | Root directory for tuner state. Defaults to `{TempPath}/navpathfinder`. |

---

### ServiceCollectionExtensions

Extension methods for ASP.NET Core / generic host dependency injection.

```csharp
public static class ServiceCollectionExtensions
```

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `AddNavPathfinder(this IServiceCollection services, Action<NavPathfinderOptions>? configure = null)` | `IServiceCollection` | Registers NavPathfinder singletons. See registered services below. |

**Registered singletons:**

| Type | DI key | Description |
|------|--------|-------------|
| `PathfindingWorld` | — | Root factory. Inject for multiple named service instances. |
| `IMultiAgentService` | `"default"` | Multi-agent tick service. |
| `INavMeshBakerService` | `"baker"` | NavMesh baking service. |
| `ISingleAgentService` | `"single"` | Single-agent query service. |
| `DynamicObstacleService` | `"obstacles"` | Dynamic obstacle update service. |
| `InfluenceMapService` | `"influence"` | Influence map computation service. |

**Usage:**
```csharp
builder.Services.AddNavPathfinder(options =>
{
    options.LicenseKey     = config["NavPathfinder:LicenseKey"];
    options.TunerStatePath = "./navpathfinder";
});
```

> For multiple named service instances (e.g. separate levels), inject `PathfindingWorld`
> directly and call `CreateMultiAgentService("level-name")`.
