using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ExecuteApplicationImportHandler(IAggregateExecutor executor,
    IAggregateReader reader, ITenantActivity tenants, TimeProvider clock, IRequestBus bus) : IRequestHandler<ExecuteApplicationImport>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ExecuteApplicationImport> context, CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only the trusted system actor may execute an import."));
        var request = context.Request;
        if (!await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The tenant is not active; retry after reactivation.", isTransient: true));
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } key || batch.SourceNamespace is not { } space)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        var source = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId, key, space), ct).ConfigureAwait(false);
        if (source.GetState(batch) is "canceled" or "committed" or "failed")
            return Result.Success;
        if (source.GetFrozenPlan(batch.Id) is not { } plan)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import has no durable personal acceptance plan."));
        foreach (var row in plan.Rows)
        {
            ct.ThrowIfCancellationRequested();
            if (!await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The tenant is not active; retry after reactivation.", isTransient: true));
            source = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId, key, space), ct).ConfigureAwait(false);
            if (source.GetState(batch) is "canceled" or "committed" or "failed")
                return Result.Success;
            var applied = await bus.SendAsync(new ApplyApplicationImportEffect(request.TenantId,
                batch.Id, row.RowId, row.ApplicationId), context, ct).ConfigureAwait(false);
            if (!applied.IsSuccess)
                return applied.Error.IsTransient ? applied : await FailAsync(row.RowId, "target_effect_rejected").ConfigureAwait(false);
        }
        if (!await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The tenant is not active; retry after reactivation.", isTransient: true));
        // Commit rehydrates the ledger through the executor, serializing with cancellation and other batches.
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var id in plan.Rows.Select(row => row.ApplicationId).Distinct())
            targets.Add(id, await reader.HydrateApplicationAsync(request.TenantId, id, ct).ConfigureAwait(false));
        var committed = await executor.ExecuteAsync(new ApplicationImportLedger(request.TenantId, key, space), ledger =>
            ledger.GetState(batch) is "canceled" or "committed" or "failed" ? AggregateOutcome.CommitOnSuccess(Result.Success) :
                AggregateOutcome.CommitOnSuccess(ledger.Commit(batch, ledger.GetRevision(batch), targets, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
        return committed.IsSuccess || committed.Error.IsTransient ? committed :
            await FailAsync(null, "commit_verification_rejected").ConfigureAwait(false);

        ValueTask<Result> FailAsync(Uuid? rowId, string code) => executor.ExecuteAsync(
            new ApplicationImportLedger(request.TenantId, key, space), ledger => AggregateOutcome.CommitOnSuccess(
                ledger.Fail(batch, plan.PlanSha256, rowId, code, clock.GetUtcNow())), context, ct);
    }
}
