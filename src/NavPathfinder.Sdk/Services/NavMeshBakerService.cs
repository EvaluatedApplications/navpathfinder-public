using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Licensing;
using NavPathfinder.Baking;
using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Wraps a <see cref="NavMeshBakerPipeline"/> and exposes a clean level-load API.
///
/// Converts SDK-facing geometry inputs (plain lists) into the immutable data record
/// expected by the baking pipeline, runs the full four-stage bake, then wraps the
/// resulting domain <c>NavMesh</c> in a <see cref="NavMeshHandle"/> for SDK consumers.
///
/// Construction is intentionally <c>internal</c>; callers obtain instances
/// via <see cref="PathfindingWorld.CreateNavMeshBakerService"/>.
/// </summary>
public sealed class NavMeshBakerService : INavMeshBakerService
{
    private readonly NavMeshBakerPipeline _pipeline;

    internal NavMeshBakerService(LicenseMode licenseMode, string? tunerStorePath)
    {
        _pipeline = NavMeshBakerPipelineBuilder.Build(licenseMode, tunerStorePath);
    }

    /// <summary>
    /// Bakes a <see cref="NavMeshHandle"/> from raw level geometry.
    ///
    /// Mapping contract:
    ///   vertices     → ImmutableArray&lt;Vector2&gt;              (vertex world positions)
    ///   triangles    → ImmutableArray&lt;(int A, int B, int C)&gt; (index triples into vertices)
    ///   blockedZones → ImmutableArray&lt;ImmutableArray&lt;Vector2&gt;&gt; (obstacle polygon outlines)
    ///
    /// The returned <see cref="NavMeshHandle"/> is immediately usable with
    /// <see cref="MultiAgentService.TickAsync"/>.
    /// </summary>
    /// <param name="vertices">Non-empty list of vertex world-space positions.</param>
    /// <param name="triangles">Non-empty list of index triples referencing <paramref name="vertices"/>.</param>
    /// <param name="blockedZones">
    ///   Optional list of obstacle polygon outlines. Each polygon is a list of at least 3 vertices.
    ///   Triangles whose centroids fall inside any zone are excluded from the NavMesh.
    ///   Pass null or empty to include all triangles.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="NavMeshHandle"/> wrapping the baked NavMesh.</returns>
    /// <exception cref="ArgumentException">Thrown when vertices or triangles are null or empty.</exception>
    public async Task<NavMeshHandle> BakeAsync(
        IReadOnlyList<Vector2>                      vertices,
        IReadOnlyList<(int A, int B, int C)>        triangles,
        IReadOnlyList<IReadOnlyList<Vector2>>?       blockedZones = null,
        CancellationToken                           ct           = default)
    {
        if (vertices is null || vertices.Count == 0)
            throw new ArgumentException(
                "Vertex list must contain at least one vertex.", nameof(vertices));

        if (triangles is null || triangles.Count == 0)
            throw new ArgumentException(
                "Triangle list must contain at least one triangle.", nameof(triangles));

        // Map SDK inputs → domain data record (all collections become immutable)
        var data = new NavMeshBakeData(
            Vertices:     vertices.ToImmutableArray(),
            Triangles:    triangles.ToImmutableArray(),
            BlockedZones: (blockedZones ?? Array.Empty<IReadOnlyList<Vector2>>())
                              .Select(z => z.ToImmutableArray())
                              .ToImmutableArray());

        // Run the full four-stage baking pipeline
        var navMesh = await _pipeline.BakeAsync(data, ct);

        // Wrap the domain NavMesh in the SDK handle — internal type never leaks
        return NavMeshHandle.Wrap(navMesh);
    }
}
