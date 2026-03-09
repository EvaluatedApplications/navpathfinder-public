namespace NavPathfinder.Sdk.Models;

/// <summary>
/// The result of a single pathfinding tick.
///
/// Replaces the previous bare <c>IReadOnlyList&lt;PathResultDto&gt;</c> return from
/// <see cref="Services.MultiAgentService.TickAsync"/> to include a pressure gauge
/// alongside the per-agent paths.
/// </summary>
/// <param name="Paths">
/// One <see cref="PathResultDto"/> per input agent, in the same order.
/// </param>
/// <param name="Pressure">
/// EMA-smoothed float in [0, 1] representing how loaded the pathfinding system is
/// relative to the <c>TargetFrameMs</c> supplied in <see cref="TickOptions"/>.
///
/// <list type="table">
///   <listheader><term>Value</term><description>Meaning</description></listheader>
///   <item><term>0.0</term><description>System idle — well under budget; freely spawn agents.</description></item>
///   <item><term>1.0</term><description>Fully saturated — at or over budget; despawn distant/low-priority agents.</description></item>
/// </list>
///
/// Always <c>0.0</c> when no <c>TargetFrameMs</c> is provided (no reference point exists).
/// </param>
/// <param name="ElapsedMs">
/// Actual wall-clock time this tick took in milliseconds (sub-millisecond precision).
/// Use this for frame profiling. <c>Pressure</c> is the smoothed relative view of the
/// same number; <c>ElapsedMs</c> is the raw absolute value.
/// </param>
public record TickResult(
    IReadOnlyList<PathResultDto> Paths,
    float                        Pressure,
    double                       ElapsedMs);
