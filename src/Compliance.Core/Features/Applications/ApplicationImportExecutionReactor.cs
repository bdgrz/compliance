using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Replays idempotent effects and commit after a crash, from the durable source seal.</summary>
public sealed partial class ApplicationImportExecutionReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus, IAggregateReader reader)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("application_imports"), WorkloadName),
      IReactorHandler<ApplicationImportPlanSealed>
{
    public const string WorkloadName = "ApplicationImportExecutionV1";

    public async ValueTask HandleAsync(IReactorContext<ApplicationImportPlanSealed> context, CancellationToken ct)
    {
        var trigger = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != trigger.TenantId.ToString())
            throw new InvalidOperationException("An import execution must belong to its tenant workload.");
        var batch = await reader.HydrateAsync(new ImportBatch(trigger.TenantId, trigger.BatchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } key || batch.SourceNamespace is not { } space ||
            context.Source.Stream != new ApplicationImportLedger(trigger.TenantId, key, space).Stream)
            throw new InvalidOperationException("The import seal must originate on its authoritative source ledger.");
        await bus.SendReactionAsync(new ExecuteApplicationImport(trigger.TenantId, trigger.BatchId), context, ct)
            .ConfigureAwait(false);
    }
}
