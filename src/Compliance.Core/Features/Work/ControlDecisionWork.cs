using System.Globalization;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Derives control draft and retirement decisions from their authoritative aggregates.</summary>
static class ControlDecisionWork
{
    public const string DraftReview = "control_draft_review";
    public const string DraftApproval = "control_draft_approval";
    public const string RetirementReview = "control_retirement_review";
    public const string RetirementApproval = "control_retirement_approval";
    public static IReadOnlyList<string> ProjectedKinds { get; } = Array.AsReadOnly<string>(
        [DraftReview, DraftApproval, RetirementReview, RetirementApproval]);

    public static bool IsFullyProjected(IReadOnlySet<string> projectedKinds) =>
        ProjectedKinds.All(projectedKinds.Contains);

    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader, IControlDraftDirectoryReader directory,
        ControlDraftListReadConsistency? consistency, OperatingAuthority authority,
        Uuid tenantId, Uuid programId,
        DateTimeOffset now, Uuid? workItemId, bool activationEnabled, bool lifecycleEnabled,
        CancellationToken ct)
    {
        if (!activationEnabled)
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);

        var fence = ProjectionCheckpoint.Start;
        if (consistency is not null)
        {
            var captured = await consistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
            if (!captured.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(captured.Error);
            fence = captured.Value;
        }

        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(tenantId, programId, 200, cursor, ct)
                .ConfigureAwait(false);
            if (page.Items.Any(summary => summary.TenantId != tenantId ||
                                          summary.ProgramId != programId))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

            foreach (var summary in page.Items)
            {
                var control = await reader.HydrateAsync(new ControlDraft(tenantId,
                    summary.ControlId), ct).ConfigureAwait(false);
                if (!control.IsCreated || !control.IsVisible || control.TenantId != tenantId ||
                    control.ProgramId != programId)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(SourceChanged());

                var state = FromSource(tenantId, control, summary.Identifier);
                var decisions = await CandidatesAsync(state, authority, now, workItemId,
                    activationEnabled, lifecycleEnabled, ct).ConfigureAwait(false);
                if (!decisions.IsSuccess)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(decisions.Error);
                candidates.AddRange(decisions.Value);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        if (consistency is not null)
        {
            var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId, fence,
                ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(confirmed.Error);
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    internal static ControlDecisionWorkState FromSource(Uuid tenantId, ControlDraft control,
        string identifier)
    {
        var targetId = control.PendingTargetId;
        var assignments = targetId is { } pendingTarget
            ? control.GetResponsibilitySet(new ResponsibilityScope("control", control.Id,
                pendingTarget, control.Revision)).ReadAssignments().ToArray()
            : [];
        return new ControlDecisionWorkState(tenantId, control.ProgramId, control.Id, identifier,
            control.Revision, control.IsApproved, control.HasOpenDraft,
            control.PendingRetirementId is not null, targetId, control.PendingAuthorMemberId,
            control.AcceptedReviewDecisionId, control.AcceptedReviewerMemberId,
            control.LatestReviewDecisionId, control.PendingDecisionChangedAt, assignments);
    }

    internal static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> CandidatesAsync(
        ControlDecisionWorkState state, OperatingAuthority authority, DateTimeOffset now,
        Uuid? wantedWorkItemId, bool activationEnabled, bool lifecycleEnabled,
        CancellationToken ct)
    {
        if (!activationEnabled)
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);
        if (state.TenantId == Uuid.Empty || state.ProgramId == Uuid.Empty ||
            state.ControlId == Uuid.Empty || string.IsNullOrWhiteSpace(state.Identifier) ||
            state.Revision < 1 || state.Assignments is null)
            return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

        var isRetirement = state.IsRetirement;
        if (!lifecycleEnabled && (isRetirement || state.IsApproved))
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);

        var targetId = state.PendingTargetId;
        var changedAt = state.PendingDecisionChangedAt;
        if (targetId is null || changedAt is null || !isRetirement && !state.HasOpenDraft)
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);

        var approval = state.AcceptedReviewDecisionId is not null;
        var kind = (isRetirement, approval) switch
        {
            (false, false) => DraftReview,
            (false, true) => DraftApproval,
            (true, false) => RetirementReview,
            (true, true) => RetirementApproval,
        };
        var responsibilityType = approval
            ? ResponsibilityType.PolicyApprover
            : ResponsibilityType.AssignedReviewer;
        var scope = new ResponsibilityScope("control", state.ControlId, targetId.Value,
            state.Revision);
        var assignmentIds = new HashSet<Uuid>();
        var activeAssignments = new HashSet<Uuid>();
        foreach (var assignment in state.Assignments)
        {
            if (assignment is null || assignment.TenantId != state.TenantId ||
                assignment.AssignmentId == Uuid.Empty || assignment.MemberId == Uuid.Empty ||
                assignment.Scope != scope || !Enum.IsDefined(assignment.Type) ||
                assignment.EffectiveUntil is { } until && until <= assignment.EffectiveFrom ||
                !assignmentIds.Add(assignment.AssignmentId))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

            if (assignment.Type == responsibilityType && assignment.EffectiveFrom <= now &&
                (assignment.EffectiveUntil is null || now < assignment.EffectiveUntil) &&
                (assignment.RevokedAt is null || now < assignment.RevokedAt))
                activeAssignments.Add(assignment.MemberId);
        }

        var excluded = new HashSet<Uuid>();
        if (state.PendingAuthorMemberId is { } author)
            excluded.Add(author);
        if (approval && state.AcceptedReviewerMemberId is { } reviewer)
            excluded.Add(reviewer);

        var responsibleMembers = new List<Uuid>();
        foreach (var memberId in activeAssignments.Where(memberId => !excluded.Contains(memberId))
                     .OrderBy(static memberId => memberId.ToString(), StringComparer.Ordinal))
            if (await authority.HasProgramManagementPermissionAsync(state.TenantId,
                    state.ProgramId, memberId, ct).ConfigureAwait(false))
                responsibleMembers.Add(memberId);
        if (responsibleMembers.Count == 0)
            return Result<IReadOnlyList<WorkCandidate>>.Success(CreateCandidates(state,
                targetId.Value, changedAt.Value, kind, approval, isRetirement, wantedWorkItemId,
                new OperatingHolder(OperatingAuthority.ProgramManagerHolder, state.ProgramId),
                null, excluded));

        var candidates = responsibleMembers.SelectMany(memberId => CreateCandidates(state,
                targetId.Value, changedAt.Value, kind, approval, isRetirement, wantedWorkItemId,
                new OperatingHolder(OperatingAuthority.MemberHolder, memberId), memberId, excluded))
            .ToArray();
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static List<WorkCandidate> CreateCandidates(ControlDecisionWorkState state,
        Uuid targetId, DateTimeOffset changedAt, string kind, bool approval, bool isRetirement,
        Uuid? wantedWorkItemId, OperatingHolder responsible, Uuid? assignedMemberId,
        IReadOnlySet<Uuid> excluded)
    {
        var round = approval
            ? state.AcceptedReviewDecisionId?.ToString()
            : state.LatestReviewDecisionId?.ToString() ?? "initial";
        var identity = Uuid.CreateVersion5(targetId,
            $"control-decision\n{state.Revision.ToString(CultureInfo.InvariantCulture)}\n" +
            $"{kind}\n{round}\n{assignedMemberId?.ToString() ?? "program-reviewers"}");
        var workItemId = WorkCandidate.IdFor(identity, kind);
        if (wantedWorkItemId is { } wanted && wanted != workItemId)
            return [];

        var verb = approval ? "Approve" : "Review";
        var summary = isRetirement
            ? $"{verb} retirement of {state.Identifier}"
            : $"{verb} {state.Identifier} control draft";
        var route = approval && isRetirement
            ? "retirements"
            : $"draft/{(approval ? "approvals" : "reviews")}";
        var reason = isRetirement
            ? $"Control retirement revision {state.Revision} is awaiting an independent " +
              $"{(approval ? "approval" : "review")}."
            : $"Control draft revision {state.Revision} is awaiting an independent " +
              $"{(approval ? "approval" : "review")}" +
              (approval ? " after its accepted review." : ".");
        return
        [
            new WorkCandidate(workItemId, kind, targetId, state.ControlId, null, summary, reason,
                null, null, approval ? "approve" : "review",
                $"/api/v1/tenants/{state.TenantId}/programs/{state.ProgramId}/" +
                $"controls/{state.ControlId}/{route}", responsible, null, excluded, changedAt),
        ];
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control work projection has an invalid program scope.");

    static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        "The control work source and directory differ. Retry the query.", isTransient: true);
}
