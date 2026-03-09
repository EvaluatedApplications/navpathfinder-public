# NavPathfinder™

**Multi-agent pathfinding and crowd simulation SDK for .NET 8. Thousands of agents. One API. No configuration.**

NavPathfinder handles everything your game or simulation needs for agent movement: shortest-path queries, full crowd simulation with collision avoidance, dynamic obstacles, influence maps, and emergent surrounding behaviour — all in one SDK that self-tunes to your hardware.

---

## Why NavPathfinder?

### Unity's built-in NavMesh stops scaling — NavPathfinder doesn't

Unity's NavMesh agent system runs on the main thread. At 200+ agents it starts eating your frame budget. At 500+ agents it becomes your frame budget. NavPathfinder runs async, ticks in parallel, and adapts its work to stay within whatever frame time you give it — automatically. The same code that runs 200 agents in the editor runs 2,000 in a shipping build with no changes.

### One API covers every pathfinding need

Most game projects end up stitching together a pathfinding library, a crowd simulation solution, an influence map system, and custom logic for surrounding/encirclement. NavPathfinder ships all of these as a single SDK with a unified service model. Bake a navmesh once. All services share it.

### Self-tuning frame budget

`PathfindingMode.Tuned` keeps your pathfinding inside a target frame time — automatically. The engine learns how many agents it can process per tick on your specific hardware and scales accordingly. There is no "max agents" constant to tune, no thread pool to size. It finds the right number on first deployment and keeps verifying it as conditions change.

---

## How it compares

| | NavPathfinder | Unity NavMesh Agents | Recast/Detour (Godot) |
|--|--------------|---------------------|----------------------|
| **Threading** | Fully async, parallel | Main thread | Main thread |
| **Frame budget management** | Automatic (Tuned mode) | Manual (priority / splitting) | None |
| **Agents at 60fps** | Scales to thousands | ~200 before frame spikes | ~150 |
| **Dynamic obstacles** | First-class, auto-reroute | Carve / rebake | Manual rebake |
| **Influence maps** | Built-in | None | None |
| **Surrounding / encirclement** | Built-in | None | None |
| **Single + crowd API** | Unified | Separate concepts | Separate |
| **.NET async** | Native `ValueTask<T>` | None | Via bindings |

---

## 5-minute quickstart

```csharp
// 1. Create the world once at application startup
var world = new PathfindingWorld(licenseKey: "YOUR-LICENSE-KEY",
                                 tunerStatePath: "./navpathfinder");

// 2. Bake a navmesh from your level geometry (once per level load)
var baker   = world.CreateNavMeshBakerService("level-1-baker");
NavMeshHandle navMesh = await baker.BakeAsync(vertices, triangles, blockedZones);

// 3. Create a service per scene
var service = world.CreateMultiAgentService("level-1");

// 4. Tick every frame — the SDK does the rest
int tick = 0;
while (gameRunning)
{
    TickResult result = await service.TickAsync(agents, navMesh, tick++,
        new TickOptions(Mode: PathfindingMode.Tuned, TargetFrameMs: 16.67));

    foreach (var path in result.Paths)
        if (path.PathFound)
            MoveAgent(path.AgentId, path.Waypoints);
}
```

**Free tier:** `new PathfindingWorld()` with no license key. All pathfinding runs correctly — single-threaded. No license required for development or low-volume production use.

---

## Framework examples

### Unity

```csharp
public class NavPathfinderManager : MonoBehaviour
{
    private PathfindingWorld  _world;
    private NavMeshHandle     _navMesh;
    private IMultiAgentService _service;
    private int _tick;

    async void Start()
    {
        _world   = new PathfindingWorld(licenseKey: PlayerPrefs.GetString("NavLicense"),
                                        tunerStatePath: Application.persistentDataPath + "/navpathfinder");
        _navMesh = await _world.CreateNavMeshBakerService("scene")
                               .BakeAsync(GetVertices(), GetTriangles(), GetObstacles());
        _service = _world.CreateMultiAgentService("scene");
    }

    async void Update()
    {
        var result = await _service.TickAsync(GetAgentDtos(), _navMesh, _tick++,
            new TickOptions(Mode: PathfindingMode.Tuned, TargetFrameMs: 16.67));

        foreach (var path in result.Paths)
            if (path.PathFound)
                AgentRegistry[path.AgentId].SetWaypoints(path.Waypoints);
    }
}
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
        _world   = new PathfindingWorld(licenseKey: ProjectSettings.GetSetting("nav_license").AsString(),
                                        tunerStatePath: OS.GetUserDataDir() + "/navpathfinder");
        _navMesh = await _world.CreateNavMeshBakerService("level")
                               .BakeAsync(GetVertices(), GetTriangles());
        _service = _world.CreateMultiAgentService("level");
    }

    public override async void _Process(double delta)
    {
        var result = await _service.TickAsync(BuildAgentList(), _navMesh, _tick++,
            new TickOptions(Mode: PathfindingMode.Tuned, TargetFrameMs: 16.67));

        foreach (var path in result.Paths)
            if (path.PathFound)
                GetAgent(path.AgentId).Navigate(path.Waypoints);
    }
}
```

### ASP.NET Core / simulation server

```csharp
// Register as singleton — one world for the application lifetime
builder.Services.AddSingleton(_ => new PathfindingWorld(
    licenseKey:     config["NavPathfinder:LicenseKey"],
    tunerStatePath: "./navpathfinder"));

// In your simulation loop
for (int tick = 0; ; tick++)
{
    var result = await service.TickAsync(agents, navMesh, tick);
    ProcessPaths(result.Paths);
    await Task.Delay(16);
}
```

---

## What you get

### Agent setup

```csharp
var agents = units.Select(u => new AgentDto(
    Id:          u.Id,
    Position:    u.Position,
    Goal:        u.Destination,
    Radius:      u.Radius,
    MaxSpeed:    u.Speed,
    GoalChanged: u.DestinationChanged   // set false when unchanged — reuses cached path at zero cost
)).ToList();
```

> **Single biggest performance tip:** set `GoalChanged = false` when the destination hasn't changed. Cached paths are reused at zero CPU cost.

### Reading results

```csharp
foreach (var path in result.Paths)
{
    if (path.PathFound && path.Waypoints.Length > 0)
        agent.SteerToward(path.Waypoints[0]);
}

// result.Pressure  — 0.0–1.0 load signal (use to scale agent population)
// result.ElapsedMs — actual tick wall time in milliseconds
```

### Scheduling modes

```csharp
// Tuned — stays within your target frame time, automatic (recommended for shipping)
new TickOptions(Mode: PathfindingMode.Tuned, TargetFrameMs: 16.67)

// Explicit — you control the knobs
new TickOptions(Mode: PathfindingMode.Explicit, MaxNewFieldsPerFrame: 5, ConflictRadius: 20f)

// Default — full fidelity, no limits (development and benchmarking)
await service.TickAsync(agents, navMesh, tick);
```

### Pressure gauge — dynamic agent scaling

`result.Pressure` (0.0–1.0) reflects how loaded the system is relative to your frame budget:

```csharp
// Simple — despawn when busy
if (result.Pressure > 0.8f) DespawnFurthestAgent();

// Proportional — scale population to load
int target = (int)(maxAgents * (1f - result.Pressure));
AdjustPopulationTo(target);
```

### Dynamic obstacles — live level changes

```csharp
var obstacleService = world.CreateDynamicObstacleService("obstacles");

// On door close, cave-in, or any geometry change
navMesh = await obstacleService.UpdateAsync(navMesh, new[]
{
    new NavMeshBlockedZone(Center: doorPosition,   Radius: 1.5f),
    new NavMeshBlockedZone(Center: rubblePosition, Radius: 3.0f),
});

// All agents automatically reroute on the next tick — no manual cache invalidation
```

### Influence maps — threat, territory, fog

```csharp
var influenceService = world.CreateInfluenceMapService("threat");

IReadOnlyList<float> threat = await influenceService.ComputeAsync(navMesh, new[]
{
    new InfluenceSourceDto(enemyPos,  Strength: 1.0f, FalloffRadius: 40f),
    new InfluenceSourceDto(turretPos, Strength: 2.0f, FalloffRadius: 25f),
});
// threat[triangleId] = 0.0 (safe) → higher values = more dangerous

// Use it: steer agents away from high-threat triangles
agent.Goal = threat.ArgMin(t => t);
```

### Surrounding mode — emergent encirclement

When multiple agents share the same goal, `SurroundingMode` makes them approach from varied angles without any application-side coordination:

```csharp
var opts = new TickOptions { SurroundingMode = true, MaxDirectChasers = 2 };
await service.TickAsync(agents, navMesh, tick, opts);
// 2 agents approach directly; the rest fan out and encircle automatically
```

Requires a licensed `PathfindingWorld`.

### Single-agent queries

```csharp
var singleService = world.CreateSingleAgentService("queries");

// Batch path queries — e.g. cutscene planning, AI evaluation, threat mapping
var paths = await singleService.QueryAsync(navMesh, new[]
{
    (start: heroPos,  goal: exitPos),
    (start: enemyPos, goal: heroPos),
});

if (paths[0] != null) TriggerCutsceneAlong(paths[0]);
```

---

## Installation

Contact us for a distribution package and license key. We'll get you set up.

---

## Licensing

| Mode | What you get |
|------|-------------|
| **Unlicensed** (free) | All pathfinding runs correctly, single-threaded. `SurroundingMode` not available. No license required — ever. Ideal for development and prototyping. |
| **Licensed** | Full parallel engine. Adaptive frame-budget tuner. Scales to thousands of agents. `SurroundingMode`. Persistent warm-start tuning. |

```csharp
// Free — correct, sequential
var world = new PathfindingWorld();

// Licensed — full parallel engine
var world = new PathfindingWorld(licenseKey: "YOUR-LICENSE-KEY");
```

Contact us for a license key.

---

## Use cases

- **Games** — RTS units, RPG crowds, tower defense waves, open-world NPC traffic
- **Simulation** — indoor space simulation, pedestrian flow, evacuation modelling
- **Robotics and autonomous systems** — multi-agent route planning over 2D floor plans
- **Testing and AI training** — high-throughput headless simulation with thousands of agents

---

## Best practices

- One `PathfindingWorld` per application — register as a singleton.
- One `MultiAgentService` per logical scene, not one per agent.
- Reuse `NavMeshHandle` across frames — it is immutable and cheap to hold.
- Do not clear `tunerStatePath` between sessions — the SDK warms up once and is at optimal performance from the second run onward.
- `TickAsync` is not safe to call concurrently on the same service instance — tick serially, or use separate service instances for independent scenes.

---

## Full API reference

[`src/NavPathfinder.Sdk/README.md`](src/NavPathfinder.Sdk/README.md)

---

*© 2026 Evaluated Applications. All rights reserved. NavPathfinder™ is a trademark of Evaluated Applications. The SDK, its design, and associated documentation are original works protected under copyright. Unauthorised reproduction or claim of authorship is prohibited.*
