using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Revalidates source-bound exception eligibility for the current personal approval attempt.</summary>
sealed class EvidenceRedactionWaiverRead(IAggregateReader reader, ITenantMembershipDirectoryReader memberships,
    IMemberAccessEligibility sourceMembers, IPermissionAuthorizer permissions)
{
    public async ValueTask<Result> RequireAsync(SeparationOfDutiesWaiver waiver, EvidenceRedaction redaction,
        Uuid beneficiary, DateTimeOffset at, CancellationToken ct)
    {
        if (redaction.CurrentPreparation is not { } preparation || !waiver.IsRecorded ||
            waiver.TenantId != preparation.Original.TenantId || waiver.RequestedAt < preparation.PreparedAt ||
            !waiver.Allows(redaction.WaiverScope(preparation), beneficiary, at) || waiver.ApproverMemberId is not { } approverId)
            return Deny();
        var approver = await reader.HydrateAsync(Member.ForVerification(waiver.TenantId, approverId), ct).ConfigureAwait(false);
        if (!approver.IsRegistered || approver.IsSuspended || approver.IsDeprovisioned || approver.Affiliation != "client_personnel" ||
            approver.UserId == Uuid.Empty || approver.Id != approverId || approver.Id != RbacIds.Member(waiver.TenantId, approver.UserId) ||
            !await sourceMembers.IsEligibleAsync(waiver.TenantId, approver.UserId, ct).ConfigureAwait(false))
            return Deny();
        var current = await memberships.GetAsync(waiver.TenantId.ToString(), approver.UserId, ct).ConfigureAwait(false);
        if (current is not { Affiliation: "client_personnel", IsSuspended: false, IsDeprovisioned: false } ||
            current.TenantId != waiver.TenantId || current.UserId != approver.UserId ||
            !await permissions.IsAllowedAsync(waiver.TenantId, approver.UserId, approverId, RbacPermissions.TenantRbacManage, ct).ConfigureAwait(false))
            return Deny();
        return Result.Success;
    }

    static Result Deny() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "The exact-scope waiver is not currently eligible for this decision."));
}
