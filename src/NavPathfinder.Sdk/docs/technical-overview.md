# NavPathfinder SDK — Technical Overview

NavPathfinder SDK provides autonomous agent navigation for real-time games and simulations
running on .NET 8. It handles the full lifecycle: bake a navigation mesh from your world
geometry, query paths for individual agents, and advance large agent populations each frame
with built-in collision avoidance and adaptive scheduling.

---

## What it does

| Capability | Description |
|------------|-------------|
| Navigation mesh baking | Convert raw world geometry (vertices + triangles) into an opaque `NavMeshHandle` used for all subsequent queries. Optionally exclude circular blocked zones. |
| Single-agent path queries | Find an optimal smoothed path between two world-space positions. Supports individual and batched queries. |
| Multi-agent population tick | Advance a full agent population by one game tick. Returns per-agent waypoints and a load indicator (`Pressure`). |
| Dynamic obstacle updates | Rebuild walkability at runtime when doors open, walls are destroyed, or physics objects block passage. |
| Influence mapping | Compute per-triangle float values (threat, territory, coverage) from a set of positioned emitters with falloff. |
| Agent separation | Push-apart pass for agents that have physically overlapped after a tick. |
| Combat resolution | Spatial combat: given attackers and defenders, compute casualties within an engagement radius. |
| Adaptive multi-population simulation | High-level API for games with multiple independent agent populations (e.g. enemies + civilians + vehicles). |

---

## Architecture — SDK abstraction layers

```
┌─────────────────────────────────────────────────────────────────┐
│  Your game / application                                        │
│                                                                 │
│   PathfindingWorld  ←  root factory, one per app               │
│         │                                                       │
│   ┌─────┼──────────────────────────────────────────────┐       │
│   │     │           PUBLIC SDK SURFACE                  │       │
│   │  INavMeshBakerService     ISingleAgentService       │       │
│   │  IMultiAgentService       DynamicObstacleService    │       │
│   │  InfluenceMapService      ISeparationService        │       │
│   │  ICombatService           IAdaptivePathfindingService│      │
│   │  ISimulationService       NavSim builder            │       │
│   └─────────────────────────────────────────────────────┘       │
│                          │                                      │
│                   NavMeshHandle (opaque)                        │
│                          │                                      │
│              [internal engine — not exposed]                    │
└─────────────────────────────────────────────────────────────────┘
```

The SDK exposes only interfaces, records, enums, and factory entry points. No internal
engine types, spatial data structures, or algorithm classes appear in the public API.

---

## Entry points

### PathfindingWorld

The root factory. Create one instance per application. All services are obtained from it;
each service is keyed by a string name so multiple independent instances (e.g. separate
game levels) can coexist with isolated tuner state.

```csharp
// Unlicensed — sequential engine, free
var world = new PathfindingWorld();

// Licensed — parallel adaptive engine
var world = new PathfindingWorld(licenseKey: "YOUR-LICENSE-KEY");
```

### NavSim builder

High-level entry point for multi-population simulations. Declare populations once, tick them
together each frame:

```csharp
ISimulationService sim = NavSim.Create(world)
    .WithFrameBudget(16.67)
    .AddPopulation("enemies",   maxCount: 200, computeWeight: 1.0f)
    .AddPopulation("civilians", maxCount: 100, computeWeight: 0.3f)
    .Build();
```

---

## NavMeshHandle

An opaque handle to a baked navigation mesh.

- **Immutable** after construction — safe to hold and reuse across frames.
- **Thread-safe** — can be passed freely between threads.
- **`TriangleCount`** — number of walkable triangles in this mesh. Use for diagnostic
  logging, mesh validation, or influence array allocation.

Construction paths:

| Method | When to use |
|--------|-------------|
| `INavMeshBakerService.BakeAsync(vertices, triangles, blockedZones)` | Standard: bake from raw level geometry |
| `NavMeshHandle.FromTriangles(definitions)` | Direct: construct from pre-computed triangle definitions |

---

## Baking — world geometry → NavMeshHandle

Call once at level load. The result is reused for the lifetime of the level.

```csharp
var baker   = world.CreateNavMeshBakerService("level-1-baker");
NavMeshHandle navMesh = await baker.BakeAsync(vertices, triangles, blockedZones);
```

**Input:**
- `vertices` — `IReadOnlyList<Vector2>` of world-space positions.
- `triangles` — `IReadOnlyList<(int A, int B, int C)>` of index triples referencing `vertices`.
- `blockedZones` (optional) — polygons whose contained triangles are excluded from the mesh.

**Output:** A `NavMeshHandle` ready for all subsequent service calls.

Rebake when the level geometry changes fundamentally. For runtime obstacle changes
(doors, destructible walls), use `DynamicObstacleService.UpdateAsync()` instead —
it is far cheaper than a full rebake.

---

## Single-agent path queries

Use `ISingleAgentService` when each agent has a distinct destination and there is no benefit
from grouping them — e.g. scripted sequences, cutscenes, or small agent counts.

```csharp
var svc = world.CreateSingleAgentService("single");

// Single query
IReadOnlyList<Vector2> waypoints = await svc.QueryPathAsync(navMesh, start, goal);

// Batch query (runs in parallel internally)
IReadOnlyList<IReadOnlyList<Vector2>> results = await svc.QueryPathBatchAsync(navMesh, queries);
```

An empty waypoint list means start or goal is outside the mesh, or no route exists.

---

## Multi-agent population tick

Use `IMultiAgentService` for large agent populations. Agents sharing the same goal
automatically share the cost of computing guidance toward it.

### Per-frame pattern

```csharp
var svc = world.CreateMultiAgentService("level-1");

// Each frame:
var agents = units.Select(u => new AgentDto(
    Id:          u.Id,
    Position:    u.Position,
    Goal:        u.Destination,
    Radius:      u.Radius,
    MaxSpeed:    u.Speed,
    GoalChanged: u.DestinationChanged   // ← most important performance setting
)).ToList();

TickResult result = await svc.TickAsync(agents, navMesh, tickNumber);
```

**`GoalChanged = false`** when an agent's destination has not changed since the last tick.
The SDK reuses the cached result at zero cost. This is the highest-impact performance
setting available.

### Reading results

```csharp
foreach (var path in result.Paths)
{
    if (path.PathFound)
        agent.SetWaypoints(path.Waypoints);   // IReadOnlyList<Vector2>
}

float pressure  = result.Pressure;   // 0.0 (idle) → 1.0 (saturated)
double elapsed  = result.ElapsedMs;  // wall-clock time for this tick
```

### Scheduling modes (TickOptions)

Pass `null` for unrestricted computation. Use `TickOptions` to apply a frame budget:

| Mode | When to use |
|------|-------------|
| `None` (default) | Prototyping, small agent counts |
| `Tuned` | Production: set `TargetFrameMs`, SDK manages everything else |
| `Explicit` | Advanced: set `MaxNewFieldsPerFrame` and `ConflictRadius` directly |

```csharp
// Recommended for production
var opts = new TickOptions(Mode: PathfindingMode.Tuned, TargetFrameMs: 16.67);
await svc.TickAsync(agents, navMesh, tick, opts);
```

### Pressure gauge

`result.Pressure` (0.0–1.0) reflects system load relative to `TargetFrameMs`. Use it to
drive population scaling:

```csharp
// Proportional population target
int target = (int)(maxAgents * (1f - result.Pressure));
AdjustPopulationTo(target);
```

Pressure is always `0.0` when no `TargetFrameMs` is provided.

### SurroundingMode (licensed)

When `SurroundingMode = true` in `TickOptions`, agents sharing a goal are automatically
directed via distinct approach corridors, producing emergent encirclement:

```csharp
var opts = new TickOptions(
    Mode:             PathfindingMode.Tuned,
    SurroundingMode:  true,
    MaxDirectChasers: 1);    // 1 agent chases directly; rest flank
```

Requires a licensed `PathfindingWorld`. Silently ignored in unlicensed mode.

---

## Dynamic obstacles

Update mesh walkability at runtime (doors, destructible walls, physics bodies):

```csharp
var obstacleSvc = world.CreateDynamicObstacleService("obstacles");

// When walkability changes, pass the COMPLETE set of currently active zones:
var zones = new[]
{
    new NavMeshBlockedZone(Center: doorPos,   Radius: 1.5f),
    new NavMeshBlockedZone(Center: rubblePos, Radius: 3.0f),
};
navMesh = await obstacleSvc.UpdateAsync(navMesh, zones);

// Agents automatically reroute on the next tick
result = await multiAgentSvc.TickAsync(agents, navMesh, tick);
```

Pass the **complete** set of active zones each call — not just deltas.

---

## Influence maps

Per-triangle float values for AI spatial reasoning (threat, territory, resource proximity):

```csharp
var influenceSvc = world.CreateInfluenceMapService("threat");

var sources = new[]
{
    new InfluenceSourceDto(enemyPos,  Strength: 1.0f, FalloffRadius: 40f),
    new InfluenceSourceDto(turretPos, Strength: 2.0f, FalloffRadius: 25f),
};

IReadOnlyList<float> threat = await influenceSvc.ComputeAsync(navMesh, sources);
// threat[i] = combined influence at triangle i; 0 = unaffected
```

Index by triangle ID. Sources outside the mesh are silently excluded.

---

## Coordinate system

- All positions use `System.Numerics.Vector2` (X, Y in world units).
- For 3D games: map your XZ ground plane to `(x, z)` → `Vector2(x, z)`.
- No world scale is assumed. The SDK operates in whatever unit system you use.

---

## Performance characteristics

### Baking

- Called once per level at load time. Not intended for per-frame use.
- Time scales with input geometry complexity (vertex and triangle count).
- The resulting `NavMeshHandle` is immutable and thread-safe once constructed.

### Single-agent queries

- Cost scales with path length, not mesh size.
- Batch queries run concurrently; total time is dominated by the longest individual query.
- Results for repeated (start, goal) pairs are cached within the service instance.

### Multi-agent tick

- **Goal-sharing amortisation:** agents sharing a goal share the cost of computing guidance
  toward it. A population of 500 agents with one common destination computes guidance once.
- **Cache hits are free:** a goal computed in a previous tick costs approximately zero in the
  current tick for all agents sharing it. Set `GoalChanged = false` to exploit this.
- **`TriangleCount` and mesh resolution:** higher triangle counts improve path accuracy but
  increase memory usage and per-tick computation. `navMesh.TriangleCount` lets you validate
  the baked mesh resolution in tests or diagnostics.
- **Collision avoidance cost:** scales with neighbourhood density. Use `ConflictRadius` to
  cap participation to nearby agents.

---

## Threading model

| Operation | Thread safety |
|-----------|--------------|
| `PathfindingWorld` construction | Thread-safe |
| `NavMeshHandle` reads | Thread-safe (immutable) |
| `IMultiAgentService.TickAsync` | **Not** safe to call concurrently on the same instance. Call serially, or use separate service instances. |
| `ISingleAgentService.QueryPathAsync` | Safe for concurrent calls |
| `INavMeshBakerService.BakeAsync` | Safe for concurrent calls across separate instances |

---

## Licensing

| Mode | Capabilities |
|------|-------------|
| **Unlicensed** (free) | Sequential execution, all core features, no SurroundingMode |
| **Licensed** | Parallel adaptive engine, SurroundingMode, adaptive self-tuner, persistent warm-start |

```csharp
// Unlicensed (free)
var world = new PathfindingWorld();

// Licensed
var world = new PathfindingWorld(licenseKey: "YOUR-LICENSE-KEY");
Console.WriteLine(world.LicenseMode);   // LicenseMode.Licensed
```

The adaptive self-tuner (licensed) converges on first run and persists its state to
`tunerStatePath` between sessions. On subsequent runs it starts from the optimal
configuration immediately.
