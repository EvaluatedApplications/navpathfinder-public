using System.Collections.Immutable;
using NavPathfinder.Domain;

namespace NavPathfinder.Sdk.Models;

/// <summary>
/// An opaque handle to a baked navigation mesh.
/// Callers never interact with the underlying mesh representation directly.
///
/// Construction paths:
///   • <see cref="FromTriangles"/> — build from SDK-facing geometry definitions (direct use)
///   • Baker services             — obtain via <see cref="Abstractions.INavMeshBakerService.BakeAsync"/>
/// </summary>
public sealed class NavMeshHandle
{
    private NavMeshHandle(NavMesh mesh) => Internal = mesh;

    // ── SDK-internal helpers ─────────────────────────────────────────────────────

    /// <summary>Wraps an existing baked mesh. For SDK-internal and test use only.</summary>
    internal static NavMeshHandle Wrap(NavMesh mesh) => new(mesh);

    /// <summary>The underlying navigation mesh. Never exposed in the public API.</summary>
    internal NavMesh Internal { get; }

    /// <summary>Number of walkable triangles in this mesh.</summary>
    public int TriangleCount => Internal.Triangles.Length;

    // ── Public factory ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="NavMeshHandle"/> from SDK-facing triangle definitions.
    /// </summary>
    /// <param name="triangles">Non-empty list of triangle definitions.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="triangles"/> is null or empty.
    /// </exception>
    public static NavMeshHandle FromTriangles(IReadOnlyList<NavMeshTriangleDefinition> triangles)
    {
        if (triangles is null || triangles.Count == 0)
            throw new ArgumentException(
                "Triangle list must contain at least one triangle.", nameof(triangles));

        // Map SDK definitions → domain triangles (internal type; never leaves this assembly)
        var navTriangles = triangles
            .Select(t => new NavMeshTriangle(
                t.Id,
                t.A,
                t.B,
                t.C,
                t.NeighbourIds.ToImmutableArray()))
            .ToImmutableArray();

        return new NavMeshHandle(new NavMesh(navTriangles));
    }
}
