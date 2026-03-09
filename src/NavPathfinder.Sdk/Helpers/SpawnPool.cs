using System.Collections.Immutable;

namespace NavPathfinder.Sdk.Helpers;

public record SpawnPoolOptions(float MinSpacing = 1f);

public static class SpawnPool
{
    public static ImmutableArray<Vector2> GetSpawnPoints(
        IReadOnlyList<Vector2> candidates,
        int count,
        SpawnPoolOptions? options = null)
    {
        if (candidates.Count == 0 || count <= 0)
            return ImmutableArray<Vector2>.Empty;

        float minSpacing2 = options?.MinSpacing ?? 1f;
        minSpacing2 *= minSpacing2;

        var result = ImmutableArray.CreateBuilder<Vector2>(count);
        var selected = new List<Vector2>(count);

        foreach (var candidate in candidates)
        {
            if (result.Count >= count) break;

            bool tooClose = false;
            foreach (var existing in selected)
            {
                if (Vector2.DistanceSquared(candidate, existing) < minSpacing2)
                { tooClose = true; break; }
            }

            if (!tooClose)
            {
                result.Add(candidate);
                selected.Add(candidate);
            }
        }

        return result.ToImmutable();
    }
}
