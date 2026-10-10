using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class AcceptApplicationImportHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock,
    IApplicationChangeImpactReader? impactReader = null) : IRequestHandler<AcceptApplicationImport>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AcceptApplicationImport> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Acceptance requires a personal Bdgrz user over HTTP."));
        var request = context.Request;
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } key || batch.SourceNamespace is not { } space)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        var source = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId, key, space), ct).ConfigureAwait(false);
        var frozen = source.GetFrozenPlan(batch.Id);
        var retirementImpacts = new Dictionary<Uuid, ApplicationChangePreview>();
        IReadOnlyList<ApplicationImportPlannedRow> plannedRows;
        if (frozen is not null)
            plannedRows = frozen.Rows;
        else
        {
            var missing = source.GetMissingSourceClaims(batch);
            if (!missing.IsSuccess)
                return Result.Failure(missing.Error);
            if (missing.Value.Count > 0)
            {
                var proposal = source.GetRetirementProposal(batch);
                if (proposal is null || impactReader is null)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "Missing source claims require a current sealed retirement proposal and complete impact evidence."));
                foreach (var retirement in proposal.Rows)
                {
                    var impact = await impactReader.ReadAsync(new PreviewApplicationChange(request.TenantId,
                        retirement.ApplicationId, retirement.ExpectedApplicationRevision, "retire"), userId, ct)
                        .ConfigureAwait(false);
                    if (!impact.IsSuccess)
                        return Result.Failure(impact.Error);
                    if (!ApplicationImportRetirementImpact.IsUsable(impact.Value, request.TenantId, retirement))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "Retirement is blocked because complete current impact and reliance evidence is unavailable."));
                    retirementImpacts.Add(retirement.ApplicationId, impact.Value);
                }
            }
            var prepared = source.PrepareAcceptancePlan(batch, request.ExpectedBatchRevision, retirementImpacts);
            if (!prepared.IsSuccess)
                return Result.Failure(prepared.Error);
            plannedRows = prepared.Value;
        }
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var row in plannedRows.Where(row => row.Decision is "link_existing" or "retire"))
            if (!targets.ContainsKey(row.ApplicationId))
                targets.Add(row.ApplicationId, await reader.HydrateApplicationAsync(request.TenantId,
                    row.ApplicationId, ct).ConfigureAwait(false));
        var (memberId, display) = ApplicationActor.From(context);
        return await executor.ExecuteAsync(new ApplicationImportLedger(request.TenantId, key, space),
            ledger => AggregateOutcome.CommitOnSuccess(ledger.BeginAcceptance(batch,
                request.ExpectedBatchRevision, targets, memberId, display, clock.GetUtcNow(),
                retirementImpacts)), context, ct).ConfigureAwait(false);
    }
}
