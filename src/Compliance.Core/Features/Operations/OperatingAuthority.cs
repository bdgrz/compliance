using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Answers who holds and may act on operating work from source records: direct member holders,
///     current team membership, workforce persons, and program-management grants. Access roles
///     never stand in for an operating responsibility.
/// </summary>
public sealed class OperatingAuthority(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions)
{
    public const string MemberHolder = "member";
    public const string PersonHolder = "person";
    public const string TeamHolder = "team";
    public const string ProgramReviewerHolder = "program_reviewer";
    public const string ProgramRecorderHolder = "program_recorder";
    public const string RiskApproverHolder = "risk_approver";
    public const string RiskExecutiveHolder = "risk_executive";

    internal ValueTask<bool> ManagesProgramAsync(Uuid tenantId, OperationsActor actor,
        Uuid programId, CancellationToken ct) => permissions.IsAllowedAsync(tenantId, actor.UserId,
        actor.MemberId, programId, IProgramScopedRequest.ManagementPermission, ct);

    /// <summary>Whether a current client member may perform a program-management decision.</summary>
    public async ValueTask<bool> HasProgramManagementPermissionAsync(Uuid tenantId,
        Uuid programId, Uuid memberId, CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && !member.IsDeprovisioned &&
               member.Affiliation == "client_personnel" && member.UserId != Uuid.Empty &&
               await permissions.IsAllowedAsync(tenantId, member.UserId, memberId, programId,
                   IProgramScopedRequest.ManagementPermission, ct).ConfigureAwait(false);
    }

    /// <summary>Whether the member holds the responsibility directly or through current team membership.</summary>
    public async ValueTask<bool> HoldsAsync(Uuid tenantId, OperatingHolder? holder, Uuid memberId,
        CancellationToken ct)
    {
        if (holder is null)
            return false;
        if (holder.Kind == MemberHolder)
            return holder.Id == memberId;
        if (holder.Kind is ProgramReviewerHolder or ProgramRecorderHolder)
        {
            var programMember = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
                .ConfigureAwait(false);
            return programMember.IsRegistered && !programMember.IsSuspended && !programMember.IsDeprovisioned &&
                   (holder.Kind == ProgramReviewerHolder
                       ? programMember.Affiliation == "client_personnel"
                       : programMember.Affiliation != "firm_staff") &&
                   programMember.UserId != Uuid.Empty &&
                   await permissions.IsAllowedAsync(tenantId, programMember.UserId, memberId, holder.Id,
                       IProgramScopedRequest.ManagementPermission, ct).ConfigureAwait(false);
        }
        if (holder.Kind is RiskApproverHolder or RiskExecutiveHolder)
        {
            var approverMember = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
                .ConfigureAwait(false);
            if (!approverMember.IsRegistered || approverMember.IsSuspended ||
                approverMember.IsDeprovisioned || approverMember.Affiliation != "client_personnel" ||
                approverMember.UserId == Uuid.Empty)
                return false;
            var executive = await permissions.IsAllowedAsync(tenantId, approverMember.UserId,
                memberId, holder.Id, RbacPermissions.RiskAcceptExecutive, ct).ConfigureAwait(false);
            if (holder.Kind == RiskExecutiveHolder)
                return executive;
            return executive || await permissions.IsAllowedAsync(tenantId, approverMember.UserId,
                memberId, holder.Id, RbacPermissions.RiskAcceptComplianceLead, ct)
                .ConfigureAwait(false);
        }
        if (holder.Kind != TeamHolder)
            return false;
        var team = await reader.HydrateAsync(new Team(tenantId, holder.Id), ct).ConfigureAwait(false);
        if (!team.IsActive)
            return false;
        var membership = await reader.HydrateAsync(new TeamMember(tenantId, holder.Id, memberId), ct)
            .ConfigureAwait(false);
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return membership.IsAssigned && member.IsRegistered && !member.IsSuspended &&
               membership.MembershipEpisodeId == member.MembershipEpisodeId;
    }

    /// <summary>Whether the holder still exists and, for a member, is active client personnel.</summary>
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, OperatingHolder holder,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(holder);
        switch (holder.Kind)
        {
            case MemberHolder:
                var member = await reader.HydrateAsync(Member.ForVerification(tenantId, holder.Id),
                    ct).ConfigureAwait(false);
                return member.IsRegistered && !member.IsSuspended &&
                       member.Affiliation != "firm_staff";
            case PersonHolder:
                var person = await reader.HydrateAsync(new Person(tenantId, holder.Id), ct)
                    .ConfigureAwait(false);
                return person.IsCreated;
            case TeamHolder:
                var team = await reader.HydrateAsync(new Team(tenantId, holder.Id), ct)
                    .ConfigureAwait(false);
                return team.IsActive;
            default:
                return false;
        }
    }

    /// <summary>Each approved control version's exclusive end, from retirement or supersession.</summary>
    public static IReadOnlyDictionary<Uuid, DateOnly?> VersionWindows(ControlDraft? control) =>
        control is null
            ? []
            : control.ReadVersions().ToDictionary(static version => version.VersionId,
                static version => version.EffectiveUntil);
}
