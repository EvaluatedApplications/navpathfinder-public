namespace NavPathfinder.Sdk.Models;

/// <summary>
/// Controls how <see cref="Services.MultiAgentService.TickAsync"/> schedules work within a frame.
/// </summary>
public enum PathfindingMode
{
    /// <summary>Full computation every frame. No scheduling limits. (Default)</summary>
    None,

    /// <summary>Developer-controlled hard limits via <see cref="TickOptions"/>.</summary>
    Explicit,

    /// <summary>SDK auto-manages within a <see cref="TickOptions.TargetFrameMs"/> budget.</summary>
    Tuned,
}
