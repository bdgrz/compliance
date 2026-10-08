using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class EvidenceRedactionAccess(EvidenceArtifactReadAccess artifacts, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IEvidenceRedactionMutationRequest>, IRequestAuthorizer<GetEvidenceRedaction>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEvidenceRedactionMutationRequest> context, CancellationToken ct) =>
        RequireAsync(context, context.Request.TenantId, context.Request is ApproveEvidenceRedaction, ct);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<GetEvidenceRedaction> context, CancellationToken ct) =>
        artifacts.RequireMembershipAsync(context, context.Request.TenantId, ct);

    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId, bool approval, CancellationToken ct)
    {
        var member = await artifacts.RequireMembershipAsync(context, tenantId, ct).ConfigureAwait(false);
        if (!member.IsSuccess)
            return member;
        UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId);
        return await permissions.IsAllowedAsync(tenantId, userId, RbacIds.Member(tenantId, userId),
            approval ? RbacPermissions.EvidenceRedactionApprove : RbacPermissions.EvidenceRedactionPrepare, ct).ConfigureAwait(false)
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The member may not record this redaction decision."));
    }
}
