using System.Collections.Immutable;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Helpers;

public record GoalSpreadingOptions(bool AllowReuse = false);

public static class GoalSpreading
{
    public static ImmutableArray<(int AgentId, Vector2 Goal)> Spread(
        IReadOnlyList<AgentDto> agents,
        IReadOnlyList<Vector2> availableGoals,
        GoalSpreadingOptions? options = null)
    {
        if (agents.Count == 0 || availableGoals.Count == 0)
            return ImmutableArray<(int, Vector2)>.Empty;

        bool allowReuse = options?.AllowReuse ?? false;
        var assigned = new HashSet<int>();
        var result = ImmutableArray.CreateBuilder<(int AgentId, Vector2 Goal)>(agents.Count);

        foreach (var agent in agents)
        {
            int bestIdx = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < availableGoals.Count; i++)
            {
                if (!allowReuse && assigned.Contains(i)) continue;
                float d = Vector2.DistanceSquared(agent.Position, availableGoals[i]);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }

            if (bestIdx < 0 && allowReuse)
            {
                for (int i = 0; i < availableGoals.Count; i++)
                {
                    float d = Vector2.DistanceSquared(agent.Position, availableGoals[i]);
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }
            }

            if (bestIdx >= 0)
            {
                assigned.Add(bestIdx);
                result.Add((agent.Id, availableGoals[bestIdx]));
            }
        }

        return result.ToImmutable();
    }
}
