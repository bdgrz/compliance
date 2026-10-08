using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class ApproveEvidenceRedactionHandler(EvidenceRedactionAccess access, EvidenceRedactionRead read,
    EvidenceRedactionSources sources, EvidenceRedactionWaiverRead waivers, IAggregateReader reader, IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ApproveEvidenceRedaction, EvidenceRedactionView>
{
    public async ValueTask<Result<EvidenceRedactionView>> HandleAsync(IRequestContext<ApproveEvidenceRedaction> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<EvidenceRedactionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Redaction approval requires a personal HTTP submission."));
        var request = context.Request;
        var allowed = await access.RequireAsync(context, request.TenantId, true, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error);
        var snapshot = await read.GetAsync(context, request.TenantId, request.RedactionId, ct).ConfigureAwait(false);
        if (!snapshot.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(snapshot.Error);
        if (snapshot.Value.Sources.Derived.Metadata.State != EvidenceArtifactStates.Available)
            return Result<EvidenceRedactionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Approval requires the exact derived artifact to be currently available."));
        SeparationOfDutiesWaiver? waiver = null;
        ulong waiverPosition = 0;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
        {
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), ct).ConfigureAwait(false);
            waiverPosition = waiver.CommittedStreamPosition;
            if (!waiver.IsRecorded)
                return Result<EvidenceRedactionView>.Failure(new RequestError(RequestErrorKind.Forbidden, "The exact-scope waiver is not active."));
        }
        allowed = await access.RequireAsync(context, request.TenantId, true, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(allowed.Error);
        var checkedSources = await sources.CheckAsync(context, snapshot.Value.Sources, ct).ConfigureAwait(false);
        if (!checkedSources.IsSuccess)
            return Result<EvidenceRedactionView>.Failure(checkedSources.Error);
        if (waiver is not null)
        {
            var current = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiver.Id), ct).ConfigureAwait(false);
            if (current.CommittedStreamPosition != waiverPosition)
                return Result<EvidenceRedactionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The waiver changed during this request.", isTransient: true));
        }
        var at = clock.GetUtcNow();
        if (waiver is not null)
        {
            var eligible = await waivers.RequireAsync(waiver, snapshot.Value.Redaction, RbacIds.Member(request.TenantId, user), at, ct).ConfigureAwait(false);
            if (!eligible.IsSuccess)
                return Result<EvidenceRedactionView>.Failure(eligible.Error);
        }
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user), UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return await executor.ExecuteAsync(new EvidenceRedaction(request.TenantId, request.RedactionId), aggregate =>
        {
            var approved = aggregate.Approve(context.RequestId, request.ExpectedRevision, request.PreparationId,
                request.PreparedRevision, snapshot.Value.Sources.Derived.Metadata.SourcePosition, actor, at, waiver);
            return approved.IsSuccess ? AggregateOutcome.Commit(Result<EvidenceRedactionView>.Success(
                aggregate.PublicView(snapshot.Value.Sources.Original.Metadata.State, snapshot.Value.Sources.Derived.Metadata.State,
                    aggregate.CurrentApproval is not null))) : AggregateOutcome.Discard(Result<EvidenceRedactionView>.Failure(approved.Error));
        }, context, ct).ConfigureAwait(false);
    }
}
