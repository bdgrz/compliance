using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Verifies only the additive redaction waiver scope; ordinary waiver authority remains mandatory.</summary>
sealed class EvidenceRedactionWaiverScopeAuthorizer(SeparationOfDutiesWaiverAuthorizer ordinary,
    EvidenceArtifactReadAccess artifacts, EvidenceRedactionRead read, IAggregateReader reader)
    : IRequestAuthorizer<RecordSeparationOfDutiesWaiver>, IRequestAuthorizer<ApproveSeparationOfDutiesWaiver>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<RecordSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        if (context.Request.Scope.RecordType != SeparationOfDutiesRecordTypes.EvidenceRedaction)
            return Result.Success;
        var allowed = await RequireAdministrationAsync(context, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        var verified = await VerifyAsync(context, context.Request.TenantId, context.Request.Scope,
            RbacIds.Member(context.Request.TenantId, context.Request.BeneficiaryUserId), ct).ConfigureAwait(false);
        return verified.IsSuccess ? await RequireAdministrationAsync(context, ct).ConfigureAwait(false) : verified;
    }

    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ApproveSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        // Preserve ordinary authorization error ordering before transport and protected type discovery.
        var allowed = await ordinary.AuthorizeAsync(context, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Waiver approval requires personal HTTP submission."));
        var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(context.Request.TenantId, context.Request.WaiverId), ct).ConfigureAwait(false);
        if (waiver.Scope?.RecordType != SeparationOfDutiesRecordTypes.EvidenceRedaction)
            return Result.Success;
        allowed = await artifacts.RequireMembershipAsync(context, context.Request.TenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        var position = waiver.CommittedStreamPosition;
        var verified = await VerifyAsync(context, context.Request.TenantId, waiver.Scope, waiver.BeneficiaryMemberId, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return verified;
        allowed = await RequireAdministrationAsync(context, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        var current = await reader.HydrateAsync(new SeparationOfDutiesWaiver(context.Request.TenantId, context.Request.WaiverId), ct).ConfigureAwait(false);
        return current.CommittedStreamPosition == position ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Conflict, "The waiver changed during scope verification.", isTransient: true));
    }

    async ValueTask<Result> RequireAdministrationAsync(IRequestContext<ISeparationOfDutiesWaiverAdminRequest> context, CancellationToken ct)
    {
        var ordinaryAuthority = await ordinary.AuthorizeAsync(context, ct).ConfigureAwait(false);
        return ordinaryAuthority.IsSuccess ? await artifacts.RequireMembershipAsync(context, context.Request.TenantId, ct).ConfigureAwait(false) :
            ordinaryAuthority;
    }

    async ValueTask<Result> VerifyAsync(IRequestContext context, Uuid tenantId, SeparationOfDutiesWaiverScope scope, Uuid beneficiary, CancellationToken ct)
    {
        var snapshot = await read.GetAsync(context, tenantId, scope.RecordId, ct).ConfigureAwait(false);
        if (!snapshot.IsSuccess)
            return Result.Failure(snapshot.Error);
        var redaction = snapshot.Value.Redaction;
        var prepared = redaction.CurrentPreparation!;
        return scope == redaction.WaiverScope(prepared) && prepared.PreparedBy.Id == beneficiary.ToString() ? Result.Success :
            Result.Failure(new RequestError(RequestErrorKind.NotFound, "The exact redaction preparation was not found."));
    }
}
