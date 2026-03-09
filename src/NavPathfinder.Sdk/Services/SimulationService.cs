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

internal record SimTick(
    NavMeshHandle NavMesh,
    int TickNumber,
    double FrameBudgetMs,
    ImmutableArray<PopulationInput> Populations,
    ImmutableArray<SimPopWorkItem>? WorkItems = null,
    ImmutableArray<PopulationTickResult>? Results = null
);

internal record SimPopWorkItem(
    string Name,
    IReadOnlyList<AgentDto> Agents,
    NavMeshHandle NavMesh,
    int TickNumber,
    double AllocatedMs,
    float PrevPressure,
    IAdaptivePathfindingService Service,
    PopulationTickResult? Result = null
);

// ── Domain context ─────────────────────────────────────────────────────────────

internal sealed class SimulationDomainContext : DomainContext
{
    private readonly IReadOnlyDictionary<string, IAdaptivePathfindingService> _services;
    private readonly Dictionary<string, float> _pressureHistory = new();
    private readonly object _lock = new();

    public SimulationDomainContext(IReadOnlyDictionary<string, IAdaptivePathfindingService> services)
        => _services = services;

    public IAdaptivePathfindingService? GetService(string name)
        => _services.TryGetValue(name, out var svc) ? svc : null;

    public float GetPressure(string name) { lock (_lock) return _pressureHistory.GetValueOrDefault(name, 0f); }
    public void SetPressure(string name, float p) { lock (_lock) _pressureHistory[name] = p; }
}

// ── Steps ──────────────────────────────────────────────────────────────────────

internal sealed class BudgetAllocationStep : ContextPureStep<NullGlobalContext, SimulationDomainContext, SimTick>
{
    protected override ValueTask<SimTick> TransformAsync(
        SimTick data, NullGlobalContext global, SimulationDomainContext domain,
        StepContext context, CancellationToken ct)
    {
        var populations = data.Populations;
        float totalWeight = 0f;
        foreach (var p in populations)
            totalWeight += p.ComputeWeight;
        if (totalWeight <= 0f) totalWeight = 1f;

        var items = ImmutableArray.CreateBuilder<SimPopWorkItem>(populations.Length);
        foreach (var p in populations)
        {
            var svc = domain.GetService(p.Name);
            if (svc is null) continue;
            double allocatedMs = (p.ComputeWeight / totalWeight) * data.FrameBudgetMs;
            float prevPressure = domain.GetPressure(p.Name);
            items.Add(new SimPopWorkItem(p.Name, p.Agents, data.NavMesh, data.TickNumber,
                allocatedMs, prevPressure, svc));
        }

        return ValueTask.FromResult(data with { WorkItems = items.ToImmutable() });
    }
}

internal sealed class TickPopulationStep : SideEffectStep<SimPopWorkItem>
{
    protected override async ValueTask<SimPopWorkItem> ExecuteWithSideEffectsAsync(
        SimPopWorkItem item, StepContext context, CancellationToken ct)
    {
        if (item.Agents.Count == 0)
            return item with { Result = new PopulationTickResult(item.Name, Array.Empty<PathResultDto>(), 0f, 0.0) };

        var opts = new AdaptiveOptions(TargetFrameMs: item.AllocatedMs);
        var result = await item.Service.AdaptiveTickAsync(
            item.Agents, item.NavMesh, item.TickNumber, item.PrevPressure, opts, ct);

        return item with { Result = new PopulationTickResult(item.Name, result.Paths, result.Pressure, result.ElapsedMs) };
    }
}

// ── Service ────────────────────────────────────────────────────────────────────

internal sealed class SimulationService : ISimulationService
{
    private readonly double _frameBudgetMs;
    private readonly SimulationDomainContext _ctx;
    private readonly Pipeline<SimTick> _pipeline;

    internal SimulationService(
        double frameBudgetMs,
        IReadOnlyDictionary<string, IAdaptivePathfindingService> services,
        LicenseMode licenseMode)
    {
        _frameBudgetMs = frameBudgetMs;
        _ctx = new SimulationDomainContext(services);
        _pipeline = BuildPipeline(licenseMode, _ctx);
    }

    public async Task<SimTickResult> TickAsync(SimTickInput input, CancellationToken ct = default)
    {
        var tick = new SimTick(input.NavMesh, input.TickNumber, _frameBudgetMs, input.Populations);
        var result = await _pipeline.RunAsync(tick, ct);

        ImmutableArray<PopulationTickResult> results = ImmutableArray<PopulationTickResult>.Empty;
        if (result is StepResult<SimTick>.Success s && s.Data.Results is not null)
        {
            results = s.Data.Results.Value;
            foreach (var pop in results)
                _ctx.SetPressure(pop.Name, pop.Pressure);
        }

        return new SimTickResult(results);
    }

    private static Pipeline<SimTick> BuildPipeline(LicenseMode licenseMode, SimulationDomainContext ctx)
    {
        Pipeline<SimTick> pipeline = null!;
        bool isLicensed = licenseMode == LicenseMode.Licensed;

        var appBuilder = Eval.App("NavSim")
            .WithResource(ResourceKind.Cpu);
        if (isLicensed) appBuilder = appBuilder.WithTuning();

        var domain = appBuilder
            .WithContext(NullGlobalContext.Instance)
            .DefineDomain("Simulation", ctx);

        domain
            .DefineTask<SimTick>("Tick")
            .AddStep<BudgetAllocationStep>("AllocateBudget")
            .ForEach<SimPopWorkItem>(
                select:              d => d.WorkItems ?? ImmutableArray<SimPopWorkItem>.Empty,
                merge:               (outer, items) => outer with
                {
                    Results = items
                        .Where(i => i.Result is not null)
                        .Select(i => i.Result!)
                        .ToImmutableArray()
                },
                collectionName:      "Populations",
                parallelism:         isLicensed ? Tunable.ForItems() : Tunable.FixedAt(1),
                minItemsForParallel: Tunable.InlineBelow(isLicensed ? 2 : int.MaxValue),
                configure:           p => p.Gate(ResourceKind.Cpu, null, g => g
                    .AddStep<TickPopulationStep>("TickPop")))
            .Run(out pipeline);

        domain.Build();
        return pipeline;
    }
}
