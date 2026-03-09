namespace NavPathfinder.Sdk.Models;

/// <summary>
/// The pathfinding result for one agent after a single <see cref="Services.MultiAgentService.TickAsync"/> call.
/// </summary>
/// <param name="AgentId">Matches the <see cref="AgentDto.Id"/> that was passed in.</param>
/// <param name="Waypoints">
///   Ordered list of world-space positions forming the agent's path.
///   Empty when <see cref="PathFound"/> is <c>false</c>.
/// </param>
/// <param name="PathFound">
///   <c>true</c> when at least one waypoint was computed;
///   <c>false</c> when <c>GoalChanged=false</c>, when the goal lies outside the NavMesh,
///   or when no route exists between start and goal.
/// </param>
public sealed record PathResultDto(
    int                    AgentId,
    IReadOnlyList<Vector2> Waypoints,
    bool                   PathFound);
