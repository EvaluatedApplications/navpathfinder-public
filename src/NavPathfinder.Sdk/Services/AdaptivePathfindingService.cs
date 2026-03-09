using System.Collections.Immutable;
using EvalApp.Abstractions;
using EvalApp.Context;
using EvalApp.Core;
using EvalApp.Fluent;
using EvalApp.Licensing;
using EvalApp.Steps;
using EvalApp.Tuning;
using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Sdk.Services;

// ── Pipeline data ──────────────────────────────────────────────────────────────

internal record AdaptiveTickData(
    // INPUT
    IReadOnlyList<AgentDto> Agents,
    NavMeshHandle           NavMesh,
    int                     TickNumber,
    float                   PipelineLoad,
    AdaptiveOptions         Options,
    IMultiAgentService      FullService,      // wrapped service for both rep ticking and full fallback
    // STAGE
    ImmutableArray<AgentGroup>?        Groups         = null,
    IReadOnlyList<AgentDto>?           RepDtos        = null,
    TickResult?                        RepResult      = null,
    ImmutableArray<FormationWorkItem>? FormationItems = null,
    // OUTPUT
    TickResult? FinalResult = null);

internal record FormationWorkItem(
    AgentGroup                              Group,
    IReadOnlyDictionary<int, PathResultDto> RepPaths,
    IReadOnlyDictionary<int, AgentDto>      AgentLookup,
    ImmutableArray<PathResultDto>           FollowerPaths = default);

// ── Steps ──────────────────────────────────────────────────────────────────────

internal sealed class ClusterAgentsStep : PureStep<AdaptiveTickData>
{
    protected override ValueTask<AdaptiveTickData> TransformAsync(
        AdaptiveTickData data, StepContext context, CancellationToken ct)
    {
        float cellSize   = data.Options.GroupRadius;
        int   maxGroup   = data.Options.MaxGroupSize;
        float goalRadius = data.Options.GroupRadius * 2f;

        // Build spatial hash keyed by cell
        var cellHash = new Dictionary<long, List<int>>(data.Agents.Count);
        for (int i = 0; i < data.Agents.Count; i++)
        {
            var pos = data.Agents[i].Position;
            int cx = (int)(pos.X / cellSize);
            int cy = (int)(pos.Y / cellSize);
            long key = ((long)(uint)cx << 32) | (uint)cy;
            if (!cellHash.TryGetValue(key, out var list))
                cellHash[key] = list = new List<int>(4);
            list.Add(i);
        }

        var assigned = new bool[data.Agents.Count];
        var clusters  = ImmutableArray.CreateBuilder<AgentGroup>();
        var repDtos   = new List<AgentDto>(data.Agents.Count);

        for (int i = 0; i < data.Agents.Count; i++)
        {
            if (assigned[i]) continue;

            assigned[i] = true;
            var agentI         = data.Agents[i];
            var clusterIndices = new List<int> { i };
            var goalSum        = agentI.Goal;

            var pos = agentI.Position;
            int cx  = (int)(pos.X / cellSize);
            int cy  = (int)(pos.Y / cellSize);

            // Check cell and 8 neighbours for nearby unassigned agents
            for (int dx = -1; dx <= 1 && clusterIndices.Count < maxGroup; dx++)
            for (int dy = -1; dy <= 1 && clusterIndices.Count < maxGroup; dy++)
            {
                long key = ((long)(uint)(cx + dx) << 32) | (uint)(cy + dy);
                if (!cellHash.TryGetValue(key, out var neighbors)) continue;

                foreach (int j in neighbors)
                {
                    if (j == i || assigned[j]) continue;
                    if (clusterIndices.Count >= maxGroup) break;

                    var agentJ = data.Agents[j];

                    if (Vector2.Distance(agentI.Position, agentJ.Position) > cellSize) continue;

                    Vector2 currentGoalCentroid = goalSum / clusterIndices.Count;
                    if (Vector2.Distance(currentGoalCentroid, agentJ.Goal) > goalRadius) continue;

                    assigned[j] = true;
                    clusterIndices.Add(j);
                    goalSum += agentJ.Goal;
                }
            }

            var goalCenter = goalSum / clusterIndices.Count;

            // Elect representative: agent whose goal is closest to the cluster centroid
            int   repIdx  = clusterIndices[0];
            float minDist = Vector2.Distance(data.Agents[repIdx].Goal, goalCenter);
            foreach (int idx in clusterIndices)
            {
                float d = Vector2.Distance(data.Agents[idx].Goal, goalCenter);
                if (d < minDist) { minDist = d; repIdx = idx; }
            }

            var followerIds = ImmutableArray.CreateBuilder<int>(clusterIndices.Count - 1);
            foreach (int idx in clusterIndices)
                if (idx != repIdx)
                    followerIds.Add(data.Agents[idx].Id);

            clusters.Add(new AgentGroup(data.Agents[repIdx].Id, followerIds.ToImmutable(), goalCenter));
            repDtos.Add(data.Agents[repIdx]);
        }

        return ValueTask.FromResult(data with
        {
            Groups  = clusters.ToImmutable(),
            RepDtos = repDtos
        });
    }
}

internal sealed class TickRepresentativesStep : SideEffectStep<AdaptiveTickData>
{
    protected override async ValueTask<AdaptiveTickData> ExecuteWithSideEffectsAsync(
        AdaptiveTickData data, StepContext context, CancellationToken ct)
    {
        var tickOpts = new TickOptions(PathfindingMode.Tuned, TargetFrameMs: data.Options.TargetFrameMs);
        var result = await data.FullService.TickAsync(data.RepDtos!, data.NavMesh, data.TickNumber, tickOpts, ct);
        return data with { RepResult = result };
    }
}

internal sealed class ApplyFormationOffsetsStep : PureStep<FormationWorkItem>
{
    protected override ValueTask<FormationWorkItem> TransformAsync(
        FormationWorkItem item, StepContext context, CancellationToken ct)
    {
        var repId       = item.Group.RepresentativeId;
        var followerIds = item.Group.FollowerIds;

        if (!item.RepPaths.TryGetValue(repId, out var repPath)
            || !repPath.PathFound
            || repPath.Waypoints.Count == 0)
        {
            var empty = ImmutableArray.CreateBuilder<PathResultDto>(followerIds.Length);
            foreach (int id in followerIds)
                empty.Add(new PathResultDto(id, Array.Empty<Vector2>(), false));
            return ValueTask.FromResult(item with { FollowerPaths = empty.ToImmutable() });
        }

        // Compute movement direction from rep position → first waypoint
        item.AgentLookup.TryGetValue(repId, out var repAgent);
        var repPos = repAgent?.Position ?? item.Group.GoalCenter;
        var dir    = repPath.Waypoints[0] - repPos;
        float len  = dir.Length();
        if (len < 1e-5f)
        {
            dir = item.Group.GoalCenter - repPos;
            len = dir.Length();
        }
        Vector2 moveDir = len > 1e-5f ? dir / len : Vector2.UnitX;
        // Perpendicular unit vector for formation spread
        Vector2 perpDir = new Vector2(-moveDir.Y, moveDir.X);

        var result = ImmutableArray.CreateBuilder<PathResultDto>(followerIds.Length);
        for (int slot = 0; slot < followerIds.Length; slot++)
        {
            int   followerId = followerIds[slot];
            int   sign       = (slot % 2 == 0) ? 1 : -1;
            float magnitude  = (slot / 2 + 1) * 1.0f;   // ±1 cell per slot
            Vector2 offset   = perpDir * (sign * magnitude);

            var waypoints = new List<Vector2>(repPath.Waypoints.Count);
            foreach (var wp in repPath.Waypoints)
                waypoints.Add(wp + offset);

            result.Add(new PathResultDto(followerId, waypoints, true));
        }

        return ValueTask.FromResult(item with { FollowerPaths = result.ToImmutable() });
    }
}

internal sealed class MergeResultsStep : PureStep<AdaptiveTickData>
{
    protected override ValueTask<AdaptiveTickData> TransformAsync(
        AdaptiveTickData data, StepContext context, CancellationToken ct)
    {
        var repResult  = data.RepResult!;
        var repPaths   = repResult.Paths.ToDictionary(p => p.AgentId);

        var followerPaths = new Dictionary<int, PathResultDto>();
        if (data.FormationItems.HasValue)
        {
            foreach (var item in data.FormationItems.Value)
            {
                if (item.FollowerPaths.IsDefaultOrEmpty) continue;
                foreach (var fp in item.FollowerPaths)
                    followerPaths[fp.AgentId] = fp;
            }
        }

        var allPaths = new List<PathResultDto>(data.Agents.Count);
        foreach (var agent in data.Agents)
        {
            if (repPaths.TryGetValue(agent.Id, out var rp))
                allPaths.Add(rp);
            else if (followerPaths.TryGetValue(agent.Id, out var fp))
                allPaths.Add(fp);
            else
                allPaths.Add(new PathResultDto(agent.Id, Array.Empty<Vector2>(), false));
        }

        var synthetic = new TickResult(allPaths, repResult.Pressure, repResult.ElapsedMs);
        return ValueTask.FromResult(data with { FinalResult = synthetic });
    }
}

// ── Service ────────────────────────────────────────────────────────────────────

public sealed class AdaptivePathfindingService : IAdaptivePathfindingService
{
    private readonly IMultiAgentService          _fullService;
    private readonly Pipeline<AdaptiveTickData>  _pipeline;

    internal AdaptivePathfindingService(LicenseMode licenseMode, string? tunerStorePath, IMultiAgentService fullService)
    {
        _fullService = fullService;
        _pipeline    = BuildPipeline(licenseMode, tunerStorePath);
    }

    public async Task<TickResult> AdaptiveTickAsync(
        IReadOnlyList<AgentDto> agents,
        NavMeshHandle           navMesh,
        int                     tickNumber,
        float                   pipelineLoad,
        AdaptiveOptions?        options = null,
        CancellationToken       ct      = default)
    {
        if (agents.Count == 0)
            return new TickResult([], 0f, 0.0);

        var opts = options ?? new AdaptiveOptions();

        // Fast path: load below threshold → full per-agent tick (zero overhead)
        if (pipelineLoad < opts.GroupingThreshold)
            return await _fullService.TickAsync(agents, navMesh, tickNumber, new TickOptions(PathfindingMode.Tuned, TargetFrameMs: opts.TargetFrameMs), ct);

        var data   = new AdaptiveTickData(agents, navMesh, tickNumber, pipelineLoad, opts, _fullService);
        var result = await _pipeline.RunAsync(data, ct);

        if (result is StepResult<AdaptiveTickData>.Success s && s.Data.FinalResult is not null)
            return s.Data.FinalResult;

        // Fallback to full tick on pipeline failure
        return await _fullService.TickAsync(agents, navMesh, tickNumber, null, ct);
    }

    private static Pipeline<AdaptiveTickData> BuildPipeline(LicenseMode mode, string? tunerStorePath)
    {
        Pipeline<AdaptiveTickData> pipeline = null!;
        bool isLicensed = mode == LicenseMode.Licensed;

        var appBuilder = Eval.App("AdaptivePathfinding")
            .WithResource(ResourceKind.Cpu);

        if (isLicensed) appBuilder = appBuilder.WithTuning(tunerStorePath);

        var domain = appBuilder
            .WithContext(NullGlobalContext.Instance)
            .DefineDomain("Adaptive", null);

        domain
            .DefineTask<AdaptiveTickData>("AdaptiveTick")
            .Gate(ResourceKind.Cpu, null, g => g
                .AddStep<ClusterAgentsStep>("Cluster"))
            .Gate(ResourceKind.Cpu, null, g => g
                .AddStep<TickRepresentativesStep>("TickReps"))
            .ForEach<FormationWorkItem>(
                select:              data => BuildFormationWorkItems(data),
                merge:               (data, items) => data with { FormationItems = ImmutableArray.CreateRange(items) },
                collectionName:      "Groups",
                parallelism:         isLicensed ? Tunable.FixedAt(Environment.ProcessorCount) : Tunable.FixedAt(1),
                minItemsForParallel: Tunable.InlineBelow(isLicensed ? 4 : int.MaxValue),
                configure:           p => p.AddStep<ApplyFormationOffsetsStep>("FormationOffsets"))
            .AddStep<MergeResultsStep>("Merge")
            .Run(out pipeline);

        domain.Build();
        return pipeline;
    }

    private static IEnumerable<FormationWorkItem> BuildFormationWorkItems(AdaptiveTickData data)
    {
        if (data.Groups is null || data.RepResult is null)
            yield break;

        var repPaths    = data.RepResult.Paths.ToDictionary(p => p.AgentId);
        var agentLookup = data.Agents.ToDictionary(a => a.Id);

        foreach (var group in data.Groups.Value)
        {
            if (group.FollowerIds.IsEmpty) continue;
            yield return new FormationWorkItem(group, repPaths, agentLookup);
        }
    }
}
