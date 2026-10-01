using Bdgrz.Compliance.Features.ControlMappings;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Registers the coverage projections over an in-memory KV store and catches them up from the
///     in-memory event store, as the per-tenant projectors do on a worker host.
/// </summary>
static class CoverageProjections
{
    public static IServiceCollection AddCoverageProjections(this IServiceCollection services)
    {
        var kv = new InMemoryKvClient();
        services.AddSingleton<IDomainEventReader>(provider =>
            (IDomainEventReader)provider.GetRequiredService<IEventStore>());
        services.AddSingleton(new FitzControlMappingDirectoryV1(kv));
        services.AddSingleton<IControlMappingDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlMappingDirectoryV1>());
        services.AddSingleton(new FitzCriterionApplicabilityDirectoryV1(kv));
        services.AddSingleton<ICriterionApplicabilityDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>());
        services.AddScoped<CriteriaCoverageReadConsistency>();
        return services;
    }

    public static async Task CatchUpAsync(IServiceProvider provider, Uuid tenantId)
    {
        var events = provider.GetRequiredService<IDomainEventReader>();
        var mappings = provider.GetRequiredService<FitzControlMappingDirectoryV1>();
        await CatchUpAsync(events, tenantId, FitzControlMappingDirectoryV1.ProjectorName,
            FitzControlMappingDirectoryV1.SourceArea,
            await mappings.LoadCheckpointAsync(tenantId), mappings.BeginAsync,
            mappings.ApplyAsync);
        var decisions = provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>();
        await CatchUpAsync(events, tenantId, FitzCriterionApplicabilityDirectoryV1.ProjectorName,
            FitzCriterionApplicabilityDirectoryV1.SourceArea,
            await decisions.LoadCheckpointAsync(tenantId), decisions.BeginAsync,
            decisions.ApplyAsync);
    }

    static async Task CatchUpAsync(IDomainEventReader events, Uuid tenantId, string projector,
        string area, ProjectionCheckpoint checkpoint,
        Func<ProjectionBatchContext, CancellationToken, ValueTask<IProjectionBatch>> begin,
        Func<DomainEvent, CancellationToken, ValueTask> apply)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), area);
        await using var batch = await begin(new ProjectionBatchContext(
            new CheckpointIdentity(projector, pattern), checkpoint), CancellationToken.None);
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor,
                           CancellationToken.None))
        {
            await apply(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor), CancellationToken.None);
    }
}
