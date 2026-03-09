using System.Collections.Immutable;

namespace NavPathfinder.Sdk.Models;

/// <summary>
/// Groups a representative agent with its followers for population-level movement
/// operations, sharing a common goal centroid.
/// </summary>
public record AgentGroup(
    int                 RepresentativeId,
    ImmutableArray<int> FollowerIds,    // IDs of non-representative agents in this cluster
    Vector2             GoalCenter);    // centroid goal used for clustering
