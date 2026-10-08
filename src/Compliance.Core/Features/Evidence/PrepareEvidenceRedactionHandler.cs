using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class PrepareEvidenceRedactionHandler(EvidenceRedactionAccess access, EvidenceRedactionSources sources,
    IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<PrepareEvidenceRedaction, EvidenceRedactionView>
{
    public async ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<PrepareEvidenceRedaction> context, CancellationToken ct)
    {
        var request = context.Request;
        var allowed = await access.RequireAsync(context, request.TenantId, false, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error);
        var captured = await sources.CaptureAsync(context, request.TenantId, request.OriginalArtifactId, request.DerivedArtifactId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(captured.Error);
        allowed = await access.RequireAsync(context, request.TenantId, false, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error);
        var checkedSources = await sources.CheckAsync(context, captured.Value, ct).ConfigureAwait(false);
        if (!checkedSources.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(checkedSources.Error);
        UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user);
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user), UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return await executor.ExecuteAsync(new EvidenceRedaction(request.TenantId, request.RedactionId), aggregate =>
        {
            var prepared = aggregate.Prepare(context.RequestId, request.ExpectedRevision,
                EvidenceRedactionSources.Identity(captured.Value.Original), EvidenceRedactionSources.Identity(captured.Value.Derived),
                request.Provenance, request.Reason, actor, clock.GetUtcNow());
            return prepared.IsSuccess ? AggregateOutcome.Commit(Result<EvidenceRedactionView>.Success(
                aggregate.PublicView(captured.Value.Original.Metadata.State, captured.Value.Derived.Metadata.State,
                    aggregate.CurrentApproval is not null && captured.Value.Derived.Metadata.State == EvidenceArtifactStates.Available))) :
                AggregateOutcome.Discard(Result<EvidenceRedactionView>.Failure(prepared.Error));
        }, context, ct).ConfigureAwait(false);
    }
}
