using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Abstractions;

/// <summary>
/// Resolves proximity-based combat interactions between two groups of agents,
/// determining casualties on each side within a single query.
/// </summary>
public interface ICombatService
{
    ValueTask<CombatResult> ResolveAsync(
        IReadOnlyList<AgentDto> attackers,
        IReadOnlyList<AgentDto> defenders,
        CombatOptions options,
        CancellationToken ct);
}
