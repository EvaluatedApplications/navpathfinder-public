namespace NavPathfinder.Sdk.Models;

/// <summary>
/// Per-tick scheduling options passed to
/// <see cref="Services.MultiAgentService.TickAsync(System.Collections.Generic.IReadOnlyList{AgentDto}, NavMeshHandle, int, TickOptions?, System.Threading.CancellationToken)"/>.
///
/// Controls the three scheduling modes:
///
/// <b>Mode.None</b> (default — pass <c>null</c> or omit):
///   Full computation every frame. All GoalChanged agents get fresh paths.
///   Goals computed in previous ticks are served from cache at zero cost.
///
/// <b>Mode.Explicit</b>:
///   Developer sets hard limits.
///   - <see cref="MaxNewFieldsPerFrame"/>: cap on new (uncached) guidance computations per tick.
///   - <see cref="ConflictRadius"/>: skip collision avoidance for agents farther apart than this.
///   Cache hits are always free — a goal computed in a previous tick costs zero against the budget.
///
/// <b>Mode.Tuned</b>:
///   SDK auto-manages within a <see cref="TargetFrameMs"/> budget.
///   Uses per-tick timing history to derive MaxNewFieldsPerFrame automatically.
///   Falls back to stale cached paths for overflow goals (typically 1-frame latency).
///
/// <b>The key scaling property — goal sharing:</b>
///   In a tower defense with 500 creeps all heading to the same exit:
///   - Guidance toward that exit is computed on tick 1, then cached permanently.
///   - From tick 2 onward: all 500 agents share one cached result — zero recomputation.
///   - Set <c>MaxNewFieldsPerFrame=0</c> from tick 2: costs nothing — all cache hits.
///   - The SDK's goal-sharing model makes high agent counts structurally cheap for swarms.
/// </summary>
/// <param name="Mode">Which scheduling mode applies this tick.</param>
/// <param name="MaxNewFieldsPerFrame">
///   Explicit/Tuned: maximum new (uncached) guidance field computations per tick.
///   Cache hits are free and not counted. Default: unlimited.
/// </param>
/// <param name="ConflictRadius">
///   Explicit/Tuned: only agents within this world-space radius of another agent
///   participate in collision avoidance. Distant agents get correct paths but skip avoidance.
///   Default: unlimited (all agents).
/// </param>
/// <param name="TargetFrameMs">
///   Tuned mode only: desired frame time in milliseconds (e.g. 16.67 for 60fps, 33.33 for 30fps).
///   The SDK derives <c>MaxNewFieldsPerFrame</c> automatically from timing history.
///   Default: 16.67ms (60fps budget).
/// </param>
/// <param name="SurroundingMode">
///   When <c>true</c>, multi-agent groups sharing the same goal are automatically assigned
///   topologically distinct approach positions so each agent approaches via a unique corridor.
///   Produces emergent encirclement behaviour with no explicit coordination logic in application code.
///   <b>Hot-path cost when false:</b> one bool check.
///   Default: <c>false</c>.
/// </param>
/// <param name="SurroundingRadius">
///   Spatial LOD for surrounding mode. Staging assignment only runs when at least two agents
///   in the group are within this world-space distance of each other.
///   Agents already spread apart naturally approach via different corridors; the SDK skips
///   the staging analysis entirely.
///   Default: <c>float.MaxValue</c> (always apply within the group when SurroundingMode is on).
/// </param>
/// <param name="MaxDirectChasers">
///   Surrounding mode only: maximum number of agents (closest to goal first) that bypass
///   staging and chase directly. All remaining agents are assigned to flanking sectors.
///   Set to 1 for tight encirclement (one attacker, rest flank); 0 to always stage all agents.
///   Default: <c>1</c>.
/// </param>
public record TickOptions(
    PathfindingMode Mode                = PathfindingMode.None,
    int             MaxNewFieldsPerFrame = int.MaxValue,
    float           ConflictRadius      = float.MaxValue,
    double          TargetFrameMs       = 16.67,
    bool            SurroundingMode     = false,
    float           SurroundingRadius   = float.MaxValue,
    int             MaxDirectChasers    = 1);
