using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class FreezeApplicationImportOmissionProposalHandler(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<FreezeApplicationImportOmissionProposal>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<FreezeApplicationImportOmissionProposal> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Omission proposal confirmation requires a personal Bdgrz user over HTTP."));
        var request = context.Request;
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, request.TenantId, request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result.Failure(loaded.Error);
        var (batch, ledger) = loaded.Value;
        if (request.ExpectedContentSha256 != batch.ContentDigest)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import content digest changed."));
        var missing = ledger.GetMissingSourceClaims(batch);
        if (!missing.IsSuccess)
            return Result.Failure(missing.Error);
        if (missing.Value.Count > 200)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An omission proposal cannot contain more than 200 missing claims."));
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var claim in missing.Value)
            if (!targets.ContainsKey(claim.Observation.ApplicationId))
                targets.Add(claim.Observation.ApplicationId, await reader.HydrateApplicationAsync(request.TenantId,
                    claim.Observation.ApplicationId, ct).ConfigureAwait(false));
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, request.TenantId, batch, ledger, ct).ConfigureAwait(false))
            return Result.Failure(ApplicationImportOmissionSources.Changed());
        var (member, display) = ApplicationActor.From(context);
        return await executor.ExecuteAsync(new ApplicationImportLedger(request.TenantId, batch.SourceKey!, batch.SourceNamespace!),
            source => AggregateOutcome.CommitOnSuccess(source.FreezeRetirementProposal(batch,
                request.ExpectedBatchRevision, request.ExpectedSourcePosition, targets, member, display,
                request.Reason, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
