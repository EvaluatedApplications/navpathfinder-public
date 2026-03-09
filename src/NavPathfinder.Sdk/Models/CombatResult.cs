using System.Collections.Immutable;

namespace NavPathfinder.Sdk.Models;

/// <summary>
/// Holds the outcome of a combat query, listing which attacker and defender agent IDs
/// were eliminated.
/// </summary>
public record CombatResult(ImmutableArray<int> DeadAttackerIds, ImmutableArray<int> DeadDefenderIds);
