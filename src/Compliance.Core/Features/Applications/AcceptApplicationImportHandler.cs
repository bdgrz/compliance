using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class AcceptApplicationImportHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<AcceptApplicationImport>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AcceptApplicationImport> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Acceptance requires a personal Bdgrz user."));
        var request = context.Request;
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } key || batch.SourceNamespace is not { } space)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        var source = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId, key, space), ct).ConfigureAwait(false);
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        var prepared = source.PrepareAcceptancePlan(batch, request.ExpectedBatchRevision);
        // A frozen plan is replayed through BeginAcceptance without recomputing its authority.
        if (!prepared.IsSuccess && source.GetFrozenPlan(batch.Id) is null)
            return Result.Failure(prepared.Error);
        foreach (var row in prepared.IsSuccess ? prepared.Value : source.GetFrozenPlan(batch.Id)!.Rows)
            if (row.Decision == "link_existing" && !targets.ContainsKey(row.ApplicationId))
                targets.Add(row.ApplicationId, await reader.HydrateApplicationAsync(request.TenantId,
                    row.ApplicationId, ct).ConfigureAwait(false));
        var (memberId, display) = ApplicationActor.From(context);
        return await executor.ExecuteAsync(new ApplicationImportLedger(request.TenantId, key, space),
            ledger => AggregateOutcome.CommitOnSuccess(ledger.BeginAcceptance(batch,
                request.ExpectedBatchRevision, targets, memberId, display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
