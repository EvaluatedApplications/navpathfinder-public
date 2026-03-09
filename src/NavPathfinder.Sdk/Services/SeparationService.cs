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

internal record SeparationPipelineData(
    IReadOnlyList<AgentDto> Agents,
    SeparationOptions Options,
    Dictionary<long, List<int>>? CellHash = null,
    ImmutableArray<Vector2> Positions = default,
    ImmutableArray<SeparationCellWorkItem>? Cells = null,
    ImmutableArray<Vector2> Result = default);

internal record SeparationCellWorkItem(
    ImmutableArray<int> AgentIndices,
    ImmutableArray<Vector2> InitialPositions,
    float MinDistance,
    float CellSize,
    int Iterations,
    ImmutableArray<Vector2>? CorrectedPositions = null);

// ── Steps ──────────────────────────────────────────────────────────────────────

internal sealed class SepBuildSpatialHashStep : PureStep<SeparationPipelineData>
{
    protected override ValueTask<SeparationPipelineData> TransformAsync(
        SeparationPipelineData data, StepContext context, CancellationToken ct)
    {
        float cellSize = data.Options.CellSize;
        int count = data.Agents.Count;
        var positions = new Vector2[count];
        var cells = new Dictionary<long, List<int>>(count);

        for (int i = 0; i < count; i++)
        {
            var pos = data.Agents[i].Position;
            positions[i] = pos;
            int cx = (int)(pos.X / cellSize);
            int cy = (int)(pos.Y / cellSize);
            long key = ((long)(uint)cx << 32) | (uint)cy;
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = new List<int>(4);
            list.Add(i);
        }

        return ValueTask.FromResult(data with
        {
            CellHash  = cells,
            Positions = ImmutableArray.Create(positions)
        });
    }
}

internal sealed class SepPartitionAgentsStep : PureStep<SeparationPipelineData>
{
    protected override ValueTask<SeparationPipelineData> TransformAsync(
        SeparationPipelineData data, StepContext context, CancellationToken ct)
    {
        var items = ImmutableArray.CreateBuilder<SeparationCellWorkItem>(data.CellHash!.Count);

        foreach (var (_, indices) in data.CellHash!)
        {
            var agentIndices     = ImmutableArray.CreateRange(indices);
            var initialPositions = ImmutableArray.CreateRange(indices.Select(i => data.Positions[i]));
            items.Add(new SeparationCellWorkItem(
                agentIndices, initialPositions,
                data.Options.MinDistance, data.Options.CellSize, data.Options.Iterations));
        }

        return ValueTask.FromResult(data with { Cells = items.ToImmutable() });
    }
}

internal sealed class SepPushApartStep : PureStep<SeparationCellWorkItem>
{
    protected override ValueTask<SeparationCellWorkItem> TransformAsync(
        SeparationCellWorkItem item, StepContext context, CancellationToken ct)
    {
        if (item.AgentIndices.Length < 2)
            return ValueTask.FromResult(item with { CorrectedPositions = item.InitialPositions });

        var positions = item.InitialPositions.ToArray();
        float minDist  = item.MinDistance;
        float minDist2 = minDist * minDist;

        for (int iter = 0; iter < item.Iterations; iter++)
        {
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++)
            {
                Vector2 delta = positions[j] - positions[i];
                float dist2 = delta.LengthSquared();
                if (dist2 >= minDist2 || dist2 < 1e-8f) continue;

                float dist = MathF.Sqrt(dist2);
                float push = (minDist - dist) * 0.5f;
                Vector2 dir = delta / dist;
                positions[i] -= dir * push;
                positions[j] += dir * push;
            }
        }

        return ValueTask.FromResult(item with { CorrectedPositions = ImmutableArray.Create(positions) });
    }
}

internal sealed class SepMergePositionsStep : PureStep<SeparationPipelineData>
{
    protected override ValueTask<SeparationPipelineData> TransformAsync(
        SeparationPipelineData data, StepContext context, CancellationToken ct)
    {
        var result = data.Positions.ToArray();

        foreach (var cell in data.Cells!.Value)
        {
            var corrected = cell.CorrectedPositions ?? cell.InitialPositions;
            for (int k = 0; k < cell.AgentIndices.Length; k++)
                result[cell.AgentIndices[k]] = corrected[k];
        }

        return ValueTask.FromResult(data with { Result = ImmutableArray.Create(result) });
    }
}

// ── Service ────────────────────────────────────────────────────────────────────

public sealed class SeparationService : ISeparationService
{
    private readonly Pipeline<SeparationPipelineData> _pipeline;

    internal SeparationService(LicenseMode licenseMode, string? tunerStorePath)
    {
        _pipeline = BuildPipeline(licenseMode, tunerStorePath);
    }

    public async ValueTask<ImmutableArray<Vector2>> SeparateAsync(
        IReadOnlyList<AgentDto> agents,
        SeparationOptions? options,
        CancellationToken ct)
    {
        if (agents.Count < 2)
            return ImmutableArray.CreateRange(agents.Select(a => a.Position));

        var opts = options ?? new SeparationOptions();
        var data = new SeparationPipelineData(agents, opts);
        var result = await _pipeline.RunAsync(data, ct);

        if (result is StepResult<SeparationPipelineData>.Success s && !s.Data.Result.IsDefault)
            return s.Data.Result;

        throw new InvalidOperationException("Separation pipeline failed unexpectedly.");
    }

    private static Pipeline<SeparationPipelineData> BuildPipeline(LicenseMode mode, string? tunerStorePath)
    {
        Pipeline<SeparationPipelineData> pipeline = null!;
        bool isLicensed = mode == LicenseMode.Licensed;

        var appBuilder = Eval.App("Separation")
            .WithResource(ResourceKind.Cpu);

        if (isLicensed) appBuilder = appBuilder.WithTuning(tunerStorePath);

        var domain = appBuilder
            .WithContext(NullGlobalContext.Instance)
            .DefineDomain("Separation", null);

        domain
            .DefineTask<SeparationPipelineData>("Separate")
            .AddStep<SepBuildSpatialHashStep>("BuildSpatialHash")
            .AddStep<SepPartitionAgentsStep>("PartitionAgents")
            .ForEach<SeparationCellWorkItem>(
                select:              data => data.Cells!.Value.AsEnumerable(),
                merge:               (data, items) => data with { Cells = ImmutableArray.CreateRange(items) },
                collectionName:      "Cells",
                parallelism:         isLicensed ? Tunable.FixedAt(Environment.ProcessorCount) : Tunable.FixedAt(1),
                minItemsForParallel: Tunable.InlineBelow(isLicensed ? 4 : int.MaxValue),
                configure:           f => f.Gate(ResourceKind.Cpu, null, g => g
                    .AddStep<SepPushApartStep>("PushApart")))
            .AddStep<SepMergePositionsStep>("MergePositions")
            .Run(out pipeline);

        domain.Build();
        return pipeline;
    }
}
