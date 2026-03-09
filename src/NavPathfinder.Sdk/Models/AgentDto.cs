namespace NavPathfinder.Sdk.Models;

/// <summary>
/// SDK-facing snapshot of an agent for one pathfinding tick.
/// </summary>
/// <param name="Id">Stable agent identifier used to correlate input agents with output <see cref="PathResultDto"/>.</param>
/// <param name="Position">Agent's current world-space position.</param>
/// <param name="Goal">Agent's desired destination.</param>
/// <param name="Radius">Physical radius used for conflict detection. Defaults to 0.5.</param>
/// <param name="MaxSpeed">Maximum movement speed. Defaults to 3.5.</param>
/// <param name="GoalChanged">
///   Set to <c>true</c> to request a new path this tick.
///   When <c>false</c> the agent is forwarded unchanged and receives no path update.
/// </param>
public sealed record AgentDto(
    int     Id,
    Vector2 Position,
    Vector2 Goal,
    float   Radius      = 0.5f,
    float   MaxSpeed    = 3.5f,
    bool    GoalChanged = false);
