using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationImportProgressHandler(IAggregateReader reader)
    : IRequestHandler<GetApplicationImportProgress, ApplicationImportProgress>
{
    public async ValueTask<Result<ApplicationImportProgress>> HandleAsync(IRequestContext<GetApplicationImportProgress> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportLifecycleRead.LoadAsync(reader, request.TenantId,
            request.BatchId, request.MinimumRevision, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportProgress>.Failure(loaded.Error);
        var (batch, ledger) = loaded.Value;
        var plan = ledger.GetFrozenPlan(batch.Id);
        var durable = 0;
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var row in plan?.Rows ?? [])
        {
            if (!targets.TryGetValue(row.ApplicationId, out var target))
            {
                target = await reader.HydrateAsync(new DeclaredApplication(request.TenantId, row.ApplicationId), ct).ConfigureAwait(false);
                targets.Add(row.ApplicationId, target);
            }
            var effect = target.GetPendingImportEffect(batch.Id, row.RowId);
            if (effect is null)
                continue;
            if (target.Stream.Realm != request.TenantId.ToString() || target.Id != row.ApplicationId ||
                effect != new ApplicationImportEffectPending(request.TenantId, row.ApplicationId,
                    plan!.Revision, plan.PlanSha256, plan.Start, row, effect.RecordedAt))
                return Result<ApplicationImportProgress>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The target progress differs from its frozen import plan."));
            if (target.CommittedStreamPosition >= effect.Metadata.AggregateVersion)
                durable++;
        }
        var fence = await ApplicationImportLifecycleRead.CheckAsync(reader, batch, ledger, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ApplicationImportProgress>.Failure(fence.Error);
        var failure = ledger.GetFailure(batch.Id);
        var commit = ledger.GetCommit(batch.Id);
        return Result<ApplicationImportProgress>.Success(new(request.TenantId, batch.Id, ledger.GetRevision(batch),
            ledger.GetState(batch), plan?.Rows.Count ?? 0, durable, commit?.Effects.Count ?? 0,
            failure?.FailureCode, failure?.RowId, plan?.Start.StartedAt, commit?.CommittedAt));
    }
}
