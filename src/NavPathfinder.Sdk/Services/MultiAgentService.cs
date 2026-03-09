using System.Collections.Immutable;
using System.Diagnostics;
using EvalApp.Licensing;
using NavPathfinder.Domain;
using NavPathfinder.Pipeline;
using NavPathfinder.Sdk.Models;
using DomainMode = NavPathfinder.Domain.PathfindingMode;
using SdkMode    = NavPathfinder.Sdk.Models.PathfindingMode;

using NavPathfinder.Sdk.Abstractions;

namespace NavPathfinder.Sdk.Services;

/// <summary>
/// Wraps a single <c>PathfindingPipeline</c> and exposes a clean tick-level API.
///
/// One pipeline instance per service key — the EvalApp adaptive tuner persists
/// observations to <c>tunerStorePath</c> between ticks and across process restarts.
///
/// <b>Scheduling modes</b> (pass <see cref="TickOptions"/> to <see cref="TickAsync"/>):
/// <list type="bullet">
///   <item><description>
///     <b>None</b> (default, <c>null</c> options): full computation, no limits.
///   </description></item>
///   <item><description>
///     <b>Explicit</b>: developer sets <c>MaxNewFieldsPerFrame</c> and/or <c>ConflictRadius</c>.
///     Cache hits are always free against the budget.
///   </description></item>
///   <item><description>
///     <b>Tuned</b>: SDK auto-manages within <c>TargetFrameMs</c>.
///     Uses per-tick timing history to derive the maximum guidance computations that fit in the budget.
///     Starts permissive (up to <see cref="TunedInitialMaxFields"/> fields), adapts each tick.
///   </description></item>
/// </list>
///
/// Construction is intentionally <c>internal</c>; callers obtain instances
/// via <see cref="PathfindingWorld.CreateMultiAgentService"/>.
/// </summary>
public sealed class MultiAgentService : IMultiAgentService
{
    private readonly PathfindingPipeline _pipeline;
    private readonly LicenseMode _licenseMode;
    private readonly string?     _licenseKey; // retained for periodic re-validation only

    // Tuned mode state — tracks last N tick wall-times to estimate cost per new guidance computation.
    // Starts permissive (enough for any reasonable game) and adapts each frame.
    private const int TunedInitialMaxFields  = 200;
    private const int TunedHistoryWindowSize = 10;

    private int  _tunedMaxFields = TunedInitialMaxFields;
    private double _lastTickMs = 0;

    // Circular buffer of recent tick wall times (ms) — used to stabilise the Tuned estimate.
    private readonly double[] _tickHistory   = new double[TunedHistoryWindowSize];
    private int              _tickHistoryN = 0; // number of samples recorded (caps at window)

    // Pressure gauge — EMA of instant pressure (elapsedMs / targetMs), clamped 0–1.
    // α = 0.3: 5 consecutive over-budget ticks → 0.83; single spike cap → 0.30.
    private const float PressureAlpha = 0.3f;
    private float _smoothedPressure   = 0f;

    internal MultiAgentService(LicenseMode licenseMode, string? licenseKey, string? tunerStorePath)
    {
        _licenseMode = licenseMode;
        _licenseKey  = licenseKey;
        _pipeline    = PathfindingPipelineBuilder.Build(licenseMode, tunerStorePath);
    }

    /// <summary>
    /// Advances pathfinding by one tick and returns one <see cref="PathResultDto"/>
    /// per input agent.
    ///
    /// Mapping contract:
    ///   AgentDto   → Agent   (domain, internal)
    ///   Agent      → PathResultDto (SDK-facing output)
    ///
    /// PathFound is true only when CurrentPath is non-null and contains at least one waypoint.
    /// Agents with <c>GoalChanged=false</c> are forwarded unchanged and receive PathFound=false.
    ///
    /// Pass <paramref name="options"/> to apply scheduling limits (Explicit or Tuned mode).
    /// Pass <c>null</c> (default) for full computation with no limits (Mode.None).
    /// </summary>
    public async Task<TickResult> TickAsync(
        IReadOnlyList<AgentDto> agents,
        NavMeshHandle           navMesh,
        int                     tickNumber,
        TickOptions?            options = null,
        CancellationToken       ct      = default)
    {
        // Mode guard — free enum comparison, runs every tick.
        // Explicit, Tuned, and SurroundingMode are licensed features.
        if (_licenseMode == LicenseMode.Unlicensed &&
            (options?.Mode is SdkMode.Explicit or SdkMode.Tuned || options?.SurroundingMode == true))
            throw new InvalidLicenseException(
                "Explicit and Tuned scheduling modes and SurroundingMode require a valid license. " +
                "Use Mode.None or upgrade to a licensed version.");

        // Periodic HMAC re-validation — runs at most once per 60 seconds on the licensed path.
        // Unlicensed keys return immediately with no overhead.
        PathfindingWorld.Gate.CheckPeriodic(_licenseKey);
        var domainAgents = agents
            .Select(a => new Agent(
                Id:          a.Id,
                Position:    a.Position,
                Goal:        a.Goal,
                Radius:      a.Radius,
                MaxSpeed:    a.MaxSpeed,
                GoalChanged: a.GoalChanged))
            .ToImmutableArray();

        var schedule = BuildSchedule(options);

        var sw      = Stopwatch.StartNew();
        var updated = await _pipeline.TickAsync(domainAgents, navMesh.Internal, tickNumber, schedule, ct);
        double elapsedMs = sw.Elapsed.TotalMilliseconds;

        RecordTickCost(elapsedMs, options);
        float pressure = ComputePressure(elapsedMs, options);

        var paths = updated
            .Select(a => new PathResultDto(
                AgentId:   a.Id,
                Waypoints: a.CurrentPath?.ToArray() ?? Array.Empty<Vector2>(),
                PathFound: a.CurrentPath is { Length: > 0 }))
            .ToList();

        return new TickResult(paths, pressure, elapsedMs);
    }

    // ── Schedule construction ─────────────────────────────────────────────────

    private TickSchedule? BuildSchedule(TickOptions? options)
    {
        if (options is null) return null;

        // Mode.None with no optional features → pure hot path, no schedule overhead.
        if (options.Mode == SdkMode.None && !options.SurroundingMode)
            return null;

        if (options.Mode == SdkMode.Explicit)
            return new TickSchedule(
                Mode:              DomainMode.Explicit,
                MaxNewFields:      options.MaxNewFieldsPerFrame,
                ConflictRadius:    options.ConflictRadius,
                SurroundingMode:   options.SurroundingMode,
                SurroundingRadius: options.SurroundingRadius,
                MaxDirectChasers:  options.MaxDirectChasers);

        if (options.Mode == SdkMode.Tuned)
            return new TickSchedule(
                Mode:              DomainMode.Tuned,
                MaxNewFields:      _tunedMaxFields,
                ConflictRadius:    options.ConflictRadius,
                SurroundingMode:   options.SurroundingMode,
                SurroundingRadius: options.SurroundingRadius,
                MaxDirectChasers:  options.MaxDirectChasers);

        // Mode.None + SurroundingMode on: no budget limits, just surrounding.
        return new TickSchedule(
            Mode:              DomainMode.None,
            SurroundingMode:   options.SurroundingMode,
            SurroundingRadius: options.SurroundingRadius,
            MaxDirectChasers:  options.MaxDirectChasers);
    }

    private void RecordTickCost(double elapsedMs, TickOptions? options)
    {
        _lastTickMs = elapsedMs;

        if (options?.Mode != SdkMode.Tuned) return;

        // Record in circular buffer
        _tickHistory[_tickHistoryN % TunedHistoryWindowSize] = elapsedMs;
        _tickHistoryN++;

        // Adapt _tunedMaxFields after enough samples
        if (_tickHistoryN < 3) return;

        double target = options.TargetFrameMs;

        // Simple additive-increase / multiplicative-decrease:
        //   Over budget → reduce aggressively (halve, floor 1)
        //   Under budget at 80% → increase by 1 (conservative)
        if (elapsedMs > target)
            _tunedMaxFields = Math.Max(1, _tunedMaxFields / 2);
        else if (elapsedMs < target * 0.80 && _tunedMaxFields < TunedInitialMaxFields)
            _tunedMaxFields++;
    }

    /// <summary>
    /// Updates the EMA-smoothed pressure gauge and returns the current value.
    /// pressure = α × instant + (1 − α) × previous,  where instant = clamp(elapsed / target, 0, 1).
    /// Returns 0 when no TargetFrameMs is configured.
    /// </summary>
    private float ComputePressure(double elapsedMs, TickOptions? options)
    {
        // No pressure reference when Mode=None (no budget concern) or no target set
        if (options is null || options.Mode == SdkMode.None || options.TargetFrameMs <= 0)
            return 0f;

        float instant  = (float)Math.Clamp(elapsedMs / options.TargetFrameMs, 0.0, 1.0);
        _smoothedPressure = PressureAlpha * instant + (1f - PressureAlpha) * _smoothedPressure;
        return _smoothedPressure;
    }
}
