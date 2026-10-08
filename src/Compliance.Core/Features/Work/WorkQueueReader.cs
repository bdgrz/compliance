using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Joins source-derived work with recorded accountability and answers, for one actor, which
///     items exist for them. Eligibility comes from the source workflow: the holder or backup of
///     the work, directly or through current team membership, as an active client member, and never
///     a member the source's separation of duties excludes. Scoped per request, it memoizes holder,
///     membership, and eligibility answers so each is hydrated at most once.
/// </summary>
public sealed class WorkQueueReader(IAggregateReader reader, OperatingAuthority authority,
    TimeProvider clock, WorkQueueReadConsistency queueConsistency,
    ICampaignDirectoryReader? campaigns = null,
    IBoundaryDirectoryReader? boundaries = null,
    IPolicyDirectoryReader? policies = null,
    PolicyDirectoryReadConsistency? policyConsistency = null,
    ICommitmentDraftDirectoryReader? commitments = null,
    CommitmentDraftListReadConsistency? commitmentConsistency = null,
    IControlDraftDirectoryReader? controls = null,
    ControlDraftListReadConsistency? controlConsistency = null,
    ControlActivationReleaseGate? controlActivationGate = null,
    ControlLifecycleReleaseGate? controlLifecycleGate = null,
    IRiskDraftDirectoryReader? risks = null,
    RiskDraftListReadConsistency? riskConsistency = null,
    CampaignDirectoryReadConsistency? campaignConsistency = null,
    IEnumerable<IAccountableWorkItemDirectoryReader>? accountableWorkItems = null)
{
    public const int SystemEscalationDays = 7;
    public const int DefaultHorizonDays = 30;

    readonly Dictionary<(OperatingHolder Holder, Uuid MemberId), bool> _holds = [];
    readonly Dictionary<Uuid, bool> _active = [];
    readonly Dictionary<Uuid, bool> _canAuthorManagement = [];
    readonly Dictionary<(Uuid WorkItemId, Uuid MemberId), bool> _eligible = [];
    readonly Dictionary<(Uuid WorkItemId, Uuid MemberId), bool> _proxyAllowed = [];
    readonly IAccountableWorkItemDirectoryReader[] _accountableWorkItems =
        accountableWorkItems?.OrderBy(static reader => reader.ProjectorName,
            StringComparer.Ordinal).ToArray() ?? [];

    internal ValueTask<Result<WorkQueueSnapshot>> ReadAsync(Uuid tenantId, Uuid programId,
        OperationsActor actor, int horizonDays, CancellationToken ct) =>
        ReadAsync(tenantId, programId, actor, horizonDays, null, ct);

    /// <summary>Resolves one work item the actor may see, loading only that item's eligibility.</summary>
    internal async ValueTask<Result<(WorkQueueSnapshot Snapshot, WorkQueueEntry Entry)>> FindAsync(
        Uuid tenantId, Uuid programId, OperationsActor actor, Uuid workItemId,
        CancellationToken ct)
    {
        var result = await ReadAsync(tenantId, programId, actor,
            ControlCadenceSchedule.MaximumDueWithinDays, workItemId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Result<(WorkQueueSnapshot, WorkQueueEntry)>.Failure(result.Error);
        var snapshot = result.Value;
        return snapshot.Entries.Count == 1
            ? Result<(WorkQueueSnapshot, WorkQueueEntry)>.Success((snapshot, snapshot.Entries[0]))
            : Result<(WorkQueueSnapshot, WorkQueueEntry)>.Failure(WorkQueueSnapshot.NotFound());
    }

    async ValueTask<Result<WorkQueueSnapshot>> ReadAsync(Uuid tenantId, Uuid programId,
        OperationsActor actor, int horizonDays, Uuid? workItemId, CancellationToken ct)
    {
        var captured = await queueConsistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<WorkQueueSnapshot>.Failure(captured.Error);

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var projectedKinds = _accountableWorkItems.SelectMany(static reader =>
                reader.ProjectedKinds).ToHashSet(StringComparer.Ordinal);
        var work = await WorkSource.LoadAsync(reader, tenantId, programId, today,
            today.AddDays(horizonDays), now, workItemId, boundaries,
            projectedKinds, ct)
            .ConfigureAwait(false);
        if (!work.IsSuccess)
            return Result<WorkQueueSnapshot>.Failure(work.Error);
        var candidates = work.Value.ToList();
        foreach (var workItems in _accountableWorkItems)
        {
            var projected = await workItems.LoadProgramAsync(tenantId, programId, now,
                today.AddDays(horizonDays), workItemId, ct).ConfigureAwait(false);
            if (!projected.IsSuccess)
                return Result<WorkQueueSnapshot>.Failure(projected.Error);
            candidates.AddRange(projected.Value.Where(candidate =>
                workItemId is not { } wanted || candidate.WorkItemId == wanted));
        }
        if (campaigns is not null && !PolicyCampaignWork.IsFullyProjected(projectedKinds))
        {
            var consistency = campaignConsistency ?? throw new InvalidOperationException(
                "Campaign work requires campaign-directory read consistency.");
            var campaignWork = await PolicyCampaignWork.LoadAsync(reader, campaigns, consistency,
                tenantId, programId, today, ct).ConfigureAwait(false);
            if (!campaignWork.IsSuccess)
                return Result<WorkQueueSnapshot>.Failure(campaignWork.Error);
            candidates.AddRange(campaignWork.Value
                .Where(candidate => !projectedKinds.Contains(candidate.Kind) &&
                                    (workItemId is not { } wanted || candidate.WorkItemId == wanted)));
        }
        if (policies is not null && !PolicyDecisionWork.IsFullyProjected(projectedKinds))
        {
            var decisions = await PolicyDecisionWork.LoadAsync(reader, policies, policyConsistency,
                tenantId, programId, today, today.AddDays(horizonDays), workItemId, ct)
                .ConfigureAwait(false);
            if (!decisions.IsSuccess)
                return Result<WorkQueueSnapshot>.Failure(decisions.Error);
            candidates.AddRange(decisions.Value.Where(candidate =>
                !projectedKinds.Contains(candidate.Kind)));
        }
        if (commitments is not null &&
            !CommitmentDecisionWork.IsFullyProjected(projectedKinds))
        {
            var decisions = await CommitmentDecisionWork.LoadAsync(reader, commitments,
                commitmentConsistency, tenantId, programId, now, workItemId, ct)
                .ConfigureAwait(false);
            if (!decisions.IsSuccess)
                return Result<WorkQueueSnapshot>.Failure(decisions.Error);
            candidates.AddRange(decisions.Value.Where(candidate =>
                !projectedKinds.Contains(candidate.Kind)));
        }
        if (controls is not null && !ControlDecisionWork.IsFullyProjected(projectedKinds))
        {
            var decisions = await ControlDecisionWork.LoadAsync(reader, controls,
                controlConsistency, authority, tenantId, programId, now, workItemId,
                controlActivationGate?.IsEnabled == true,
                controlLifecycleGate?.IsEnabled == true, ct).ConfigureAwait(false);
            if (!decisions.IsSuccess)
                return Result<WorkQueueSnapshot>.Failure(decisions.Error);
            candidates.AddRange(decisions.Value);
        }
        if (risks is not null && !RiskAcceptanceWork.IsFullyProjected(projectedKinds))
        {
            var decisions = await RiskAcceptanceWork.LoadAsync(reader, risks, riskConsistency,
                tenantId, programId, now, workItemId, ct).ConfigureAwait(false);
            if (!decisions.IsSuccess)
                return Result<WorkQueueSnapshot>.Failure(decisions.Error);
            candidates.AddRange(decisions.Value);
        }
        var ledger = await reader.HydrateAsync(new WorkAssignmentLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        var manages = await authority.ManagesProgramAsync(tenantId, actor, programId, ct)
            .ConfigureAwait(false);
        var entries = new List<WorkQueueEntry>();
        foreach (var candidate in candidates)
        {
            var state = ledger.Read(candidate.WorkItemId);
            var assignee = await AssigneeAsync(tenantId, candidate, state, ct).ConfigureAwait(false);
            var item = Compose(candidate, state, assignee, today);
            var eligible = await IsEligibleAsync(tenantId, candidate, actor.MemberId, ct)
                .ConfigureAwait(false);
            var inTeam = candidate.Responsible.Kind == OperatingAuthority.TeamHolder &&
                         await HoldsAsync(tenantId, candidate.Responsible, actor.MemberId, ct)
                             .ConfigureAwait(false);
            if (eligible || assignee == actor.MemberId ||
                manages && !RiskAcceptanceWork.HasRestrictedAuthority(candidate) &&
                (candidate.Kind != FindingClosureWork.Kind || eligible))
                entries.Add(new WorkQueueEntry(candidate, state, item, eligible, inTeam));
        }
        var ordered = entries
            .OrderBy(static entry => entry.Item.DueOn is null)
            .ThenBy(static entry => entry.Item.DueOn)
            .ThenBy(static entry => MaterialityRank(entry.Item.Materiality))
            .ThenBy(static entry => entry.Item.CreatedAt)
            .ThenBy(static entry => entry.Item.WorkItemId.ToString(), StringComparer.Ordinal)
            .ToArray();
        var confirmed = await queueConsistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            captured.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<WorkQueueSnapshot>.Failure(confirmed.Error);
        return Result<WorkQueueSnapshot>.Success(new WorkQueueSnapshot(today, manages, ordered));
    }

    /// <summary>
    ///     A recorded assignee who is still eligible, otherwise the direct member holder when
    ///     eligible, otherwise unassigned.
    /// </summary>
    async ValueTask<Uuid?> AssigneeAsync(Uuid tenantId, WorkCandidate candidate,
        WorkAssignmentState state, CancellationToken ct)
    {
        if (state.AssigneeMemberId is { } recorded &&
            await IsEligibleAsync(tenantId, candidate, recorded, ct).ConfigureAwait(false))
            return recorded;
        if (candidate.Responsible.Kind == OperatingAuthority.MemberHolder &&
            await IsEligibleAsync(tenantId, candidate, candidate.Responsible.Id, ct)
                .ConfigureAwait(false))
            return candidate.Responsible.Id;
        return null;
    }

    /// <summary>Whether the source workflow would accept this member performing the work.</summary>
    public async ValueTask<bool> IsEligibleAsync(Uuid tenantId, WorkCandidate candidate,
        Uuid memberId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var key = (candidate.WorkItemId, memberId);
        if (_eligible.TryGetValue(key, out var known))
            return known;
        var eligible = !candidate.Excluded.Contains(memberId) &&
                       (await HoldsAsync(tenantId, candidate.Responsible, memberId, ct)
                            .ConfigureAwait(false) ||
                        candidate.Backup is { } backup &&
                        await HoldsAsync(tenantId, backup, memberId, ct).ConfigureAwait(false)) &&
                       await IsActiveMemberAsync(tenantId, memberId, ct).ConfigureAwait(false);
        if (eligible && PolicyCampaignWork.RequiresProgramManagement(candidate))
            eligible = candidate.Backup is { Kind: OperatingAuthority.ProgramRecorderHolder } manager &&
                       await HoldsAsync(tenantId, manager, memberId, ct).ConfigureAwait(false);
        if (eligible && candidate.RequiredManagementProgramId is { } requiredProgram)
            eligible = await HoldsAsync(tenantId,
                new OperatingHolder(OperatingAuthority.ProgramManagerHolder, requiredProgram), memberId, ct)
                .ConfigureAwait(false);
        if (eligible)
            eligible = await CanAssignAsync(tenantId, candidate, memberId, ct).ConfigureAwait(false);
        _eligible[key] = eligible;
        return eligible;
    }

    internal async ValueTask<bool> CanAssignAsync(Uuid tenantId, WorkCandidate candidate,
        Uuid memberId, CancellationToken ct)
    {
        if (candidate.Kind == PolicyCampaignWork.Acknowledgement && candidate.NextAction == "record_acknowledgement")
        {
            var key = (candidate.WorkItemId, memberId);
            if (_proxyAllowed.TryGetValue(key, out var known))
                return known;
            if (candidate.AcknowledgementPerson is not { } person)
                return _proxyAllowed[key] = false;
            var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
                .ConfigureAwait(false);
            if (!member.IsRegistered || member.UserId == Uuid.Empty ||
                RbacIds.Member(tenantId, member.UserId) != memberId)
                return _proxyAllowed[key] = false;
            var eligibility = await new PolicyAcknowledgementRecorderGuard(reader,
                    new ClientManagementIndependenceGuard(reader))
                .EvaluateCapturedAsync(tenantId, member.UserId, person, ct).ConfigureAwait(false);
            return _proxyAllowed[key] = eligibility.CanRecordProxy;
        }
        return !WorkSourceManagement.IsManagementMutation(candidate) ||
               await CanAuthorManagementAsync(tenantId, memberId, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> CanAuthorManagementAsync(Uuid tenantId, Uuid memberId, CancellationToken ct)
    {
        if (_canAuthorManagement.TryGetValue(memberId, out var allowed))
            return allowed;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        allowed = member.IsRegistered && member.UserId != Uuid.Empty &&
                  RbacIds.Member(tenantId, member.UserId) == memberId &&
                  await new ClientManagementIndependenceGuard(reader)
                      .CanAuthorAsync(tenantId, member.UserId, ct).ConfigureAwait(false);
        _canAuthorManagement[memberId] = allowed;
        return allowed;
    }

    async ValueTask<bool> HoldsAsync(Uuid tenantId, OperatingHolder holder, Uuid memberId,
        CancellationToken ct)
    {
        var key = (holder, memberId);
        if (!_holds.TryGetValue(key, out var holds))
            _holds[key] = holds = await authority.HoldsAsync(tenantId, holder, memberId, ct)
                .ConfigureAwait(false);
        return holds;
    }

    async ValueTask<bool> IsActiveMemberAsync(Uuid tenantId, Uuid memberId, CancellationToken ct)
    {
        if (!_active.TryGetValue(memberId, out var active))
            _active[memberId] = active = await authority.IsActiveAsync(tenantId,
                new OperatingHolder(OperatingAuthority.MemberHolder, memberId), ct)
                .ConfigureAwait(false);
        return active;
    }

    /// <summary>The item as read. Seven days overdue escalates by system rule.</summary>
    public static WorkQueueItemView Compose(WorkCandidate candidate, WorkAssignmentState state,
        Uuid? assignee, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(state);
        var escalatedBy = state.Escalated
            ? "member"
            : candidate.DueOn is { } due && today.DayNumber - due.DayNumber >= SystemEscalationDays
                ? "system"
                : null;
        return new WorkQueueItemView(candidate.WorkItemId, candidate.Kind, candidate.SourceId,
            candidate.ControlId, candidate.FindingId, candidate.Summary, candidate.Reason,
            candidate.DueOn, candidate.DueOn < today, candidate.Materiality,
            candidate.NextAction, candidate.ActionPath, candidate.Responsible, assignee,
            state.Revision, escalatedBy is not null, escalatedBy, candidate.CreatedAt);
    }

    static int MaterialityRank(string? materiality) => materiality switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3,
    };
}
