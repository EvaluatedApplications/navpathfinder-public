namespace NavPathfinder.Sdk.Models;

/// <summary>
/// SDK-facing triangle definition used when constructing a <see cref="NavMeshHandle"/>
/// from caller-supplied geometry.
///
/// This is the only triangle type that should appear in the public NavPathfinder.Sdk API.
/// </summary>
/// <param name="Id">Unique triangle identifier; must match array index for NavMesh lookup correctness.</param>
/// <param name="A">First vertex.</param>
/// <param name="B">Second vertex.</param>
/// <param name="C">Third vertex.</param>
/// <param name="NeighbourIds">IDs of adjacent triangles that share an edge with this triangle.</param>
public sealed record NavMeshTriangleDefinition(
    int Id,
    Vector2 A,
    Vector2 B,
    Vector2 C,
    IReadOnlyList<int> NeighbourIds);
