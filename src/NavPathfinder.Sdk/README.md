# NavPathfinder SDK

High-performance multi-agent pathfinding for .NET 8. Drop-in, self-tuning, no configuration required.

---

## Installation

```xml
<PackageReference Include="NavPathfinder.Sdk" Version="1.0.0" />
```

---

## 5-minute quickstart

```csharp
// 1. Create the world once (e.g. in your game/app startup)
var world = new PathfindingWorld(tunerStatePath: "./navpathfinder");

// 2. Bake a navmesh from your level geometry (once per level)
var baker   = world.CreateNavMeshBakerService("level-1-baker");
NavMeshHandle navMesh = await baker.BakeAsync(vertices, triangles, blockedZones);

// 3. Create a service (once per scene)
var service = world.CreateMultiAgentService("level-1");

// 4. Tick every frame
int tick = 0;
while (gameRunning)
{
    TickResult result = await service.TickAsync(agents, navMesh, tick++);

    foreach (var path in result.Paths)
    {
        if (path.PathFound)
            MoveAgent(path.AgentId, path.Waypoints);
    }
}
```

---

## Framework examples

### Unity (MonoBehaviour)

```csharp
public class NavPathfinderManager : MonoBehaviour
{
    private PathfindingWorld    _world;
    private NavMeshHandle       _navMesh;
    private IMultiAgentService  _service;
    private int _tick;

    async void Start()
    {
        _world   = new PathfindingWorld(Application.persistentDataPath + "/navpathfinder");
        var baker = _world.CreateNavMeshBakerService("scene-baker");
        _navMesh = await baker.BakeAsync(GetVertices(), GetTriangles(), GetObstacles());
        _service = _world.CreateMultiAgentService("scene");
    }

    async void Update()
    {
        var agents = GetAgentDtos();  // build from your agent components
        var result = await _service.TickAsync(agents, _navMesh, _tick++);

        foreach (var path in result.Paths)
        {
            if (path.PathFound)
                AgentRegistry[path.AgentId].SetWaypoints(path.Waypoints);
        }
    }
}
```

Building `AgentDto` from a component:

```csharp
private AgentDto ToDto(MyAgent a, bool goalChanged) => new AgentDto(
    Id:          a.InstanceId,
    Position:    new Vector2(a.transform.position.x, a.transform.position.z),
    Goal:        new Vector2(a.Destination.x,        a.Destination.z),
    Radius:      a.CollisionRadius,
    MaxSpeed:    a.MaxSpeed,
    GoalChanged: goalChanged);
```

### Godot 4 (C#)

```csharp
public partial class NavPathfinderNode : Node
{
    private PathfindingWorld   _world;
    private NavMeshHandle      _navMesh;
    private IMultiAgentService _service;
    private int _tick;

    public override async void _Ready()
    {
        _world   = new PathfindingWorld(OS.GetUserDataDir() + "/navpathfinder");
        var baker = _world.CreateNavMeshBakerService("level-baker");
        _navMesh = await baker.BakeAsync(GetVertices(), GetTriangles());
        _service = _world.CreateMultiAgentService("level");
    }

    public override async void _Process(double delta)
    {
        var result = await _service.TickAsync(BuildAgentList(), _navMesh, _tick++);

        foreach (var path in result.Paths)
        {
            if (path.PathFound)
                GetAgent(path.AgentId).Navigate(path.Waypoints);
        }
    }
}
```

### ASP.NET Core / console (.NET 8)

```csharp
var world   = new PathfindingWorld("./navpathfinder");
var baker   = world.CreateNavMeshBakerService();
var service = world.CreateMultiAgentService();

var navMesh = await baker.BakeAsync(vertices, triangles);

for (int tick = 0; ; tick++)
{
    var result = await service.TickAsync(agents, navMesh, tick);
    ProcessPaths(result.Paths);
    await Task.Delay(16);  // ~60fps
}
```

### ASP.NET Core / .NET Dependency Injection

Register with `AddNavPathfinder` in `Program.cs`:

```csharp
builder.Services.AddNavPathfinder(options =>
{
    options.LicenseKey     = builder.Configuration["NavPathfinder:LicenseKey"];
    options.TunerStatePath = "./navpathfinder";
});
```

Then inject any registered service by interface:

```csharp
public class PathfindingController : ControllerBase
{
    private readonly IMultiAgentService _pathfinding;

    public PathfindingController(IMultiAgentService pathfinding)
        => _pathfinding = pathfinding;

    [HttpPost("tick")]
    public async Task<TickResult> Tick([FromBody] TickRequest req)
        => await _pathfinding.TickAsync(req.Agents, req.NavMesh, req.Tick);
}
```

**Registered singletons:**

| Interface / Type | Description |
|---|---|
| `PathfindingWorld` | Root factory — inject for multiple named service instances |
| `IMultiAgentService` | Multi-agent tick service (default key `"default"`) |
| `INavMeshBakerService` | NavMesh baking service (default key `"baker"`) |
| `ISingleAgentService` | Single-agent query service (default key `"single"`) |
| `DynamicObstacleService` | Dynamic obstacle update service (default key `"obstacles"`) |
| `InfluenceMapService` | Influence map computation service (default key `"influence"`) |

> **Named instances:** For multiple independent scenes or levels each with their own tuner state, inject `PathfindingWorld` and call `CreateMultiAgentService("level-name")` to get separate keyed instances.

---

## Agent setup

Every frame, pass one `AgentDto` per agent:

```csharp
var agents = units.Select(u => new AgentDto(
    Id:          u.Id,
    Position:    u.Position,      // Vector2 — current world position
    Goal:        u.Destination,   // Vector2 — target world position
    Radius:      u.Radius,        // collision radius
    MaxSpeed:    u.Speed,
    GoalChanged: u.DestinationChanged  // true only when the goal changed this frame
)).ToList();
```

> **Tip:** Set `GoalChanged = false` when the destination hasn't changed. This reuses the cached path at zero cost and is the most impactful performance setting available.

---

## Reading results

```csharp
TickResult result = await service.TickAsync(agents, navMesh, tick);

foreach (var path in result.Paths)
{
    path.AgentId    // int — matches the input AgentDto.Id
    path.PathFound  // bool — false if unreachable or goal unchanged
    path.Waypoints  // Vector2[] — smoothed waypoints from position to goal
}

result.Pressure   // float 0.0–1.0 — system load (see Pressure Gauge)
result.ElapsedMs  // double — tick wall time in milliseconds
```

Following waypoints:

```csharp
if (path.PathFound && path.Waypoints.Length > 0)
{
    var next = path.Waypoints[0];         // immediate next position
    var final = path.Waypoints[^1];       // final goal waypoint
    agent.SteerToward(next);
}
```

---

## Scheduling modes

Control how much computation happens per frame. Omit `TickOptions` for the default (full fidelity, no limits).

```csharp
// Default — full computation, no frame budget
await service.TickAsync(agents, navMesh, tick);

// Tuned — stay within a frame budget (recommended for production)
var opts = new TickOptions(
    Mode:          PathfindingMode.Tuned,
    TargetFrameMs: 16.67);  // 60fps budget
await service.TickAsync(agents, navMesh, tick, opts);

// Explicit — set limits manually
var opts = new TickOptions(
    Mode:                PathfindingMode.Explicit,
    MaxNewFieldsPerFrame: 5,
    ConflictRadius:       20f);
await service.TickAsync(agents, navMesh, tick, opts);
```

| Mode | Description |
|---|---|
| `None` (default) | Full computation every frame. No configuration needed. |
| `Tuned` | Set `TargetFrameMs`. The SDK auto-manages everything else. Recommended for shipping games. |
| `Explicit` | Set `MaxNewFieldsPerFrame` (new path computations per frame) and/or `ConflictRadius` (skip avoidance for distant agents). |

---

## Pressure Gauge

`result.Pressure` (0.0–1.0) tells you how loaded the pathfinding system is relative to your `TargetFrameMs`. Use it to drive scene population so your game never exceeds hardware limits.

```csharp
// Simple: despawn when busy
if (result.Pressure > 0.8f)
    DespawnFurthestAgent();

// Recommended: proportional population target
int target = (int)(maxAgents * (1f - result.Pressure));
AdjustPopulationTo(target);

// Spawn gate
if (Random.Shared.NextSingle() < (1f - result.Pressure))
    SpawnNewAgent();
```

| Pressure | Meaning |
|---|---|
| `0.0` | Plenty of headroom — spawn freely |
| `0.5` | Moderate load — hold population |
| `1.0` | Fully saturated — despawn low-priority agents |

Pressure is `0.0` unless `TargetFrameMs` is set.

---

## Dynamic obstacles

Update navmesh walkability at runtime (doors, destructible walls, physics objects):

```csharp
var obstacleService = world.CreateDynamicObstacleService("obstacles");

// When something changes, update the navmesh
var currentObstacles = new[]
{
    new NavMeshBlockedZone(Center: doorPosition,    Radius: 1.5f),
    new NavMeshBlockedZone(Center: rubblePosition,  Radius: 3.0f),
};

navMesh = await obstacleService.UpdateAsync(navMesh, currentObstacles);

// Then tick normally — agents automatically reroute
result = await service.TickAsync(agents, navMesh, tick);
```

Pass the **complete set** of currently active blocked zones each call. Call `obstacleService.ResetBase()` after a full level re-bake.

---

## Single agent / scripted paths

For cutscenes, UI path previews, or scripted NPC movement:

```csharp
var singleService = world.CreateSingleAgentService("single");

var queries = new[] { (start: heroPos, goal: doorPos) };
var paths   = await singleService.QueryAsync(navMesh, queries);

if (paths[0] != null)
    PlayCutsceneAlong(paths[0]);  // Vector2[]
```

---

## AI influence maps

Per-triangle threat/territory values for AI decision-making:

```csharp
var influenceService = world.CreateInfluenceMapService("threat");

var sources = new[]
{
    new InfluenceSourceDto(enemyPos,   Strength: 1.0f, FalloffRadius: 40f),
    new InfluenceSourceDto(turretPos,  Strength: 2.0f, FalloffRadius: 25f),
};

IReadOnlyList<float> threat = await influenceService.ComputeAsync(navMesh, sources);
// threat[triangleId] = 0 (safe) → higher (dangerous)

// Use in AI: avoid high-threat triangles when choosing patrol routes
```

---

## How it achieves performance

**Pre-compiled execution graph.** At `PathfindingWorld` construction, the internal pipeline is compiled into a single fixed async delegate. Every subsequent `TickAsync` call executes this pre-built delegate with zero virtual dispatch overhead and zero per-frame allocation on the hot path — build the slow part once, run the fast part forever.

**Zero-reflection hot path.** Internal data accessors are bound at initialisation time, not at call time. The per-stage overhead is equivalent to a direct method call.

**Concurrent path cache.** Routes between frequently-visited goal positions are stored in a concurrent structure. Cache reads are O(1). Cache entries survive across ticks for the lifetime of the service instance — repeated goals are structurally free from the second tick onward.

**Adaptive self-tuner.** The licensed engine uses a feedback controller to continuously adjust internal execution parameters based on observed frame timing. State is persisted to disk between sessions. The tuner converges on first run; subsequent runs start from the optimal state immediately.

---
## Best practices

- **One `MultiAgentService` per logical scene**, not one per agent.
- **Reuse `NavMeshHandle`** across frames — it is immutable and cheap to hold.
- **`GoalChanged = false`** when the agent's destination hasn't changed — skips recomputation for stable agents.
- **Tuner state persists.** Do not wipe `tunerStatePath` between sessions — the SDK converges on first run and is optimal from the second run onward.
- **`TickAsync` is not safe to call concurrently** on the same service instance. Tick serially, or use separate service instances with separate keys.
