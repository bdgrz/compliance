using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Independent access to one exact artifact; wider record visibility supplies no artifact authority.</summary>
sealed class EvidenceArtifactReadAccess(ITenantMembershipDirectoryReader memberships,
    IMemberAccessEligibility sourceMembers, ITenantActivity tenants, IAggregateReader reader,
    IAccessGrantScopePermissionAuthorizer permissions)
{
    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId, Uuid artifactId,
        CancellationToken ct)
    {
        var membership = await RequireMembershipAsync(context, tenantId, ct).ConfigureAwait(false);
        if (!membership.IsSuccess)
            return membership;
        if (artifactId == Uuid.Empty)
            return Missing();
        UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId);
        var scope = new AccessGrantScope(AccessGrantScopeKind.SharedResource, artifactId, EvidenceArtifactResourceTypes.Artifact);
        return await permissions.IsAllowedAtAnyScopeAsync(tenantId, userId, RbacIds.Member(tenantId, userId),
            [scope], RbacPermissions.EvidenceArtifactRead, ct).ConfigureAwait(false) ? Result.Success : Missing();
    }

    /// <summary>Verifies current owning membership before discovering any protected artifact references.</summary>
    public async ValueTask<Result> RequireMembershipAsync(IRequestContext context, Uuid tenantId, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Artifact metadata requires a canonical personal Bdgrz identity."));
        if (tenantId == Uuid.Empty)
            return Missing();
        var member = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (member is not { Affiliation: "client_personnel", IsSuspended: false, IsDeprovisioned: false } ||
            member.TenantId != tenantId || member.UserId != userId ||
            !await sourceMembers.IsEligibleAsync(tenantId, userId, ct).ConfigureAwait(false) ||
            !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Missing();
        var current = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
        if (current.UserId != userId || current.Id != RbacIds.Member(tenantId, userId) ||
            !current.IsRegistered || current.Affiliation != "client_personnel" || current.IsSuspended || current.IsDeprovisioned)
            return Missing();
        return Result.Success;
    }

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The artifact metadata was not found."));
}
