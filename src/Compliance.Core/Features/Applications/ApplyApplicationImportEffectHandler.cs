using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ApplyApplicationImportEffectHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock,
    IApplicationImportRetirementImpactReader? retirementImpactReader = null) : IRequestHandler<ApplyApplicationImportEffect>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyApplicationImportEffect> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only Portia's trusted system actor may write a pending import effect."));
        var authority = await ApplicationImportEffectAuthority.LoadAsync(reader, context.Request, ct)
            .ConfigureAwait(false);
        if (!authority.IsSuccess)
            return Result.Failure(authority.Error);
        var (batch, ledger) = authority.Value;
        var plan = ledger.GetFrozenPlan(batch.Id);
        var row = plan?.Rows.SingleOrDefault(candidate => candidate.RowId == context.Request.RowId);
        if (row?.Decision == "retire")
        {
            if (retirementImpactReader is null || row.ExpectedApplicationRevision is not { } revision)
                return RetirementImpactUnavailable();
            var impact = await retirementImpactReader.ReadForImportWorkerAsync(new PreviewApplicationChange(
                context.Request.TenantId, row.ApplicationId, revision, "retire"), ct).ConfigureAwait(false);
            if (!impact.IsSuccess)
                return RetirementImpactUnavailable();
            if (!ApplicationImportRetirementImpact.MatchesFrozen(impact.Value, context.Request.TenantId, row))
                return RetirementImpactUnavailable();
        }
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, context.Request.TenantId,
            context.Request.ApplicationId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            app => AggregateOutcome.CommitOnSuccess(app.RecordPendingImportEffect(ledger, batch,
                context.Request.RowId, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    static Result RetirementImpactUnavailable() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "Current complete retirement impact is unavailable; the import remains pending.", isTransient: true));
}
