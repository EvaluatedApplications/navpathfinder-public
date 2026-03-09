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

internal record CombatPipelineData(
    IReadOnlyList<AgentDto> Attackers,
    IReadOnlyList<AgentDto> Defenders,
    CombatOptions Options,
    Dictionary<long, List<int>>? AttackerCellHash = null,
    ImmutableArray<DefenderWorkItem>? DefenderItems = null,
    ImmutableArray<int> DeadAttackerIds = default,
    ImmutableArray<int> DeadDefenderIds = default);

internal record DefenderWorkItem(
    int DefenderIndex,
    AgentDto Defender,
    ImmutableArray<AgentDto> NearbyAttackers,
    CombatOptions Options,
    bool DefenderDied = false,
    ImmutableArray<int> KilledAttackerIds = default);

// ── Steps ──────────────────────────────────────────────────────────────────────

internal sealed class CombatBuildSpatialHashStep : PureStep<CombatPipelineData>
{
    protected override ValueTask<CombatPipelineData> TransformAsync(
        CombatPipelineData data, StepContext context, CancellationToken ct)
    {
        float cellSize = data.Options.Radius;
        var hash = new Dictionary<long, List<int>>(data.Attackers.Count);

        for (int i = 0; i < data.Attackers.Count; i++)
        {
            var pos = data.Attackers[i].Position;
            int cx = (int)(pos.X / cellSize);
            int cy = (int)(pos.Y / cellSize);
            long key = ((long)(uint)cx << 32) | (uint)cy;
            if (!hash.TryGetValue(key, out var list))
                hash[key] = list = new List<int>(4);
            list.Add(i);
        }

        return ValueTask.FromResult(data with { AttackerCellHash = hash });
    }
}

internal sealed class CombatPartitionAttackersStep : PureStep<CombatPipelineData>
{
    protected override ValueTask<CombatPipelineData> TransformAsync(
        CombatPipelineData data, StepContext context, CancellationToken ct)
    {
        float cellSize = data.Options.Radius;
        var items = ImmutableArray.CreateBuilder<DefenderWorkItem>(data.Defenders.Count);

        for (int di = 0; di < data.Defenders.Count; di++)
        {
            var defender = data.Defenders[di];
            var pos = defender.Position;
            int cx = (int)(pos.X / cellSize);
            int cy = (int)(pos.Y / cellSize);

            var nearbyAttackers = ImmutableArray.CreateBuilder<AgentDto>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                long key = ((long)(uint)(cx + dx) << 32) | (uint)(cy + dy);
                if (data.AttackerCellHash!.TryGetValue(key, out var indices))
                {
                    foreach (int ai in indices)
                        nearbyAttackers.Add(data.Attackers[ai]);
                }
            }

            items.Add(new DefenderWorkItem(di, defender, nearbyAttackers.ToImmutable(), data.Options));
        }

        return ValueTask.FromResult(data with { DefenderItems = items.ToImmutable() });
    }
}

internal sealed class CombatResolveKillsStep : PureStep<DefenderWorkItem>
{
    protected override ValueTask<DefenderWorkItem> TransformAsync(
        DefenderWorkItem item, StepContext context, CancellationToken ct)
    {
        var rng = Random.Shared;
        bool defenderDied = false;
        var killedAttackerIds = ImmutableArray.CreateBuilder<int>();

        foreach (var attacker in item.NearbyAttackers)
        {
            if (Vector2.Distance(item.Defender.Position, attacker.Position) > item.Options.Radius)
                continue;

            if (rng.NextSingle() < item.Options.DefenderKillRate)
                killedAttackerIds.Add(attacker.Id);

            if (!defenderDied && rng.NextSingle() < item.Options.AttackerKillRate)
                defenderDied = true;
        }

        return ValueTask.FromResult(item with
        {
            DefenderDied      = defenderDied,
            KilledAttackerIds = killedAttackerIds.ToImmutable()
        });
    }
}

internal sealed class CombatCollectCasualtiesStep : PureStep<CombatPipelineData>
{
    protected override ValueTask<CombatPipelineData> TransformAsync(
        CombatPipelineData data, StepContext context, CancellationToken ct)
    {
        var deadAttackerIds = new HashSet<int>();
        var deadDefenderIds = ImmutableArray.CreateBuilder<int>();

        foreach (var item in data.DefenderItems!.Value)
        {
            if (item.DefenderDied)
                deadDefenderIds.Add(item.Defender.Id);

            foreach (int id in item.KilledAttackerIds)
                deadAttackerIds.Add(id);
        }

        return ValueTask.FromResult(data with
        {
            DeadAttackerIds = ImmutableArray.CreateRange(deadAttackerIds),
            DeadDefenderIds = deadDefenderIds.ToImmutable()
        });
    }
}

// ── Service ────────────────────────────────────────────────────────────────────

public sealed class CombatService : ICombatService
{
    private readonly Pipeline<CombatPipelineData> _pipeline;

    internal CombatService(LicenseMode licenseMode, string? tunerStorePath)
    {
        _pipeline = BuildPipeline(licenseMode, tunerStorePath);
    }

    public async ValueTask<CombatResult> ResolveAsync(
        IReadOnlyList<AgentDto> attackers,
        IReadOnlyList<AgentDto> defenders,
        CombatOptions options,
        CancellationToken ct)
    {
        if (attackers.Count == 0 || defenders.Count == 0)
            return new CombatResult(ImmutableArray<int>.Empty, ImmutableArray<int>.Empty);

        var data   = new CombatPipelineData(attackers, defenders, options);
        var result = await _pipeline.RunAsync(data, ct);

        if (result is StepResult<CombatPipelineData>.Success s)
        {
            var dead = s.Data;
            return new CombatResult(
                dead.DeadAttackerIds.IsDefault ? ImmutableArray<int>.Empty : dead.DeadAttackerIds,
                dead.DeadDefenderIds.IsDefault ? ImmutableArray<int>.Empty : dead.DeadDefenderIds);
        }

        throw new InvalidOperationException("Combat pipeline failed unexpectedly.");
    }

    private static Pipeline<CombatPipelineData> BuildPipeline(LicenseMode mode, string? tunerStorePath)
    {
        Pipeline<CombatPipelineData> pipeline = null!;
        bool isLicensed = mode == LicenseMode.Licensed;

        var appBuilder = Eval.App("Combat")
            .WithResource(ResourceKind.Cpu);

        if (isLicensed) appBuilder = appBuilder.WithTuning(tunerStorePath);

        var domain = appBuilder
            .WithContext(NullGlobalContext.Instance)
            .DefineDomain("Combat", null);

        domain
            .DefineTask<CombatPipelineData>("Resolve")
            .AddStep<CombatBuildSpatialHashStep>("BuildSpatialHash")
            .AddStep<CombatPartitionAttackersStep>("PartitionAttackers")
            .ForEach<DefenderWorkItem>(
                select:              data => data.DefenderItems!.Value.AsEnumerable(),
                merge:               (data, items) => data with { DefenderItems = ImmutableArray.CreateRange(items) },
                collectionName:      "Defenders",
                parallelism:         isLicensed ? Tunable.FixedAt(Environment.ProcessorCount) : Tunable.FixedAt(1),
                minItemsForParallel: Tunable.InlineBelow(isLicensed ? 4 : int.MaxValue),
                configure:           f => f.Gate(ResourceKind.Cpu, null, g => g
                    .AddStep<CombatResolveKillsStep>("ResolveKills")))
            .AddStep<CombatCollectCasualtiesStep>("CollectCasualties")
            .Run(out pipeline);

        domain.Build();
        return pipeline;
    }
}
