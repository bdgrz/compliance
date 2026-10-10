using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Checks current membership, queue-read, access-review, and restricted-system eligibility.</summary>
public sealed class AccessReviewQueueEligibility(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer scopedPermissions, IPermissionAuthorizer permissions,
    RestrictedApplicationVisibility visibility)
{
    public async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid? programId, Uuid memberId,
        Uuid systemInstanceId, CancellationToken ct)
    {
        var member = await ActiveMemberAsync(tenantId, memberId, ct).ConfigureAwait(false);
        return member is not null && await CanReadAsync(tenantId, programId, member,
            systemInstanceId, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> CanReviewAsync(Uuid tenantId, Uuid? programId, Uuid memberId,
        Uuid systemInstanceId, CancellationToken ct)
        => await CanReadAsync(tenantId, programId, memberId, systemInstanceId, ct)
            .ConfigureAwait(false);

    public async ValueTask<bool> CanRemediateAsync(Uuid tenantId, Uuid? programId, Uuid memberId,
        Uuid systemInstanceId, CancellationToken ct)
    {
        var member = await ActiveMemberAsync(tenantId, memberId, ct).ConfigureAwait(false);
        return member is not null &&
               await CanReadAsync(tenantId, programId, member, systemInstanceId, ct)
                   .ConfigureAwait(false) &&
               await permissions.IsAllowedAsync(tenantId, member.UserId, memberId,
                   RbacPermissions.AccessReviewManage, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> IsActiveMemberAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct) =>
        await ActiveMemberAsync(tenantId, memberId, ct).ConfigureAwait(false) is not null;

    async ValueTask<Member?> ActiveMemberAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty)
            return null;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && !member.IsDeprovisioned &&
               member.Affiliation != "firm_staff" && member.UserId != Uuid.Empty &&
               RbacIds.Member(tenantId, member.UserId) == memberId
            ? member
            : null;
    }

    async ValueTask<bool> HasQueueReadAsync(Uuid tenantId, Uuid? programId, Member member,
        CancellationToken ct) => programId is { } routedProgramId
        ? await scopedPermissions.IsAllowedAsync(tenantId, member.UserId, member.Id,
            routedProgramId, IProgramReadRequest.ReadPermission, ct).ConfigureAwait(false)
        : await permissions.IsAllowedAsync(tenantId, member.UserId, member.Id,
            IProgramReadRequest.ReadPermission, ct).ConfigureAwait(false);

    async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid? programId, Member member,
        Uuid systemInstanceId, CancellationToken ct) =>
        await HasQueueReadAsync(tenantId, programId, member, ct).ConfigureAwait(false) &&
        await visibility.CanReadSystemInstanceAsync(tenantId, member.UserId,
            systemInstanceId, ct).ConfigureAwait(false);
}
