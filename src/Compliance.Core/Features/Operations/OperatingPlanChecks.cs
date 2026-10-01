using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Source-record checks shared by plan proposal and preview.</summary>
static class OperatingPlanChecks
{
    public static async ValueTask<Result<(ControlVersionView Version, bool ReviewerHoldsWork)>>
        CheckAsync(IAggregateReader reader, OperatingAuthority authority, Uuid tenantId,
            Uuid programId, Uuid controlId, Uuid controlVersionId, OperatingHolder owner,
            OperatingHolder? backupOwner, Uuid reviewerMemberId, CancellationToken ct)
    {
        var control = await ControlOperationsSource.LoadControlAsync(reader, tenantId, programId,
            controlId, ct).ConfigureAwait(false);
        if (control is null)
            return Failure(ControlOperationsSource.ControlNotFound());
        if (control.IsRetired || control.ApprovedVersion is not { Status: "approved" } version)
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an active control with an approved version can receive an operating plan."));
        if (version.VersionId != controlVersionId)
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "An operating plan must target the control's exact current approved version."));
        foreach (var holder in new[] { owner, backupOwner,
                     new OperatingHolder(OperatingAuthority.MemberHolder, reviewerMemberId) })
            if (holder is not null && holder.Id != Uuid.Empty &&
                !await authority.IsActiveAsync(tenantId, holder, ct).ConfigureAwait(false))
                return Failure(new RequestError(RequestErrorKind.Validation,
                    "Every operating holder must be an active member, an existing workforce person, or an active team."));
        var reviewerHoldsWork =
            await authority.HoldsAsync(tenantId, owner, reviewerMemberId, ct).ConfigureAwait(false) ||
            await authority.HoldsAsync(tenantId, backupOwner, reviewerMemberId, ct)
                .ConfigureAwait(false);
        return Result<(ControlVersionView, bool)>.Success((version, reviewerHoldsWork));
    }

    static Result<(ControlVersionView, bool)> Failure(RequestError error) =>
        Result<(ControlVersionView, bool)>.Failure(error);
}
