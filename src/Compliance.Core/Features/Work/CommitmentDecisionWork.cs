using System.Globalization;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Derives current commitment review and approval work from source drafts.</summary>
static class CommitmentDecisionWork
{
    public const string Review = "commitment_draft_review";
    public const string Approval = "commitment_draft_approval";
    public static IReadOnlyList<string> ProjectedKinds { get; } = Array.AsReadOnly<string>(
        [Review, Approval]);

    public static bool IsFullyProjected(IReadOnlySet<string> projectedKinds) =>
        ProjectedKinds.All(projectedKinds.Contains);

    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader, ICommitmentDraftDirectoryReader directory,
        CommitmentDraftListReadConsistency? consistency, Uuid tenantId, Uuid programId,
        DateTimeOffset now, Uuid? workItemId, CancellationToken ct)
    {
        var fence = ProjectionCheckpoint.Start;
        if (consistency is not null)
        {
            var captured = await consistency.CaptureFenceAsync(tenantId, ct).ConfigureAwait(false);
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
                var draft = await reader.HydrateAsync(new CommitmentDraft(tenantId,
                    summary.DraftId), ct).ConfigureAwait(false);
                if (!draft.IsCreated || draft.ProgramId != programId ||
                    draft.Revision != summary.Revision ||
                    !StringComparer.Ordinal.Equals(draft.Identifier, summary.Identifier))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(SourceChanged());

                var state = FromSource(tenantId, draft, summary.LastChangedAt);
                var work = Candidates(state, now, workItemId);
                if (!work.IsSuccess)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(work.Error);
                candidates.AddRange(work.Value);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        if (consistency is not null)
        {
            var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId, fence, ct)
                .ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(confirmed.Error);
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    internal static CommitmentDecisionWorkState FromSource(Uuid tenantId,
        CommitmentDraft draft, DateTimeOffset draftChangedAt)
    {
        var scope = new ResponsibilityScope(SeparationOfDutiesRecordTypes.Commitment,
            draft.Id, draft.Id, draft.Revision);
        return new CommitmentDecisionWorkState(tenantId, draft.ProgramId, draft.Id,
            draft.Identifier!, draft.Revision, draft.LatestEffectiveRevision,
            draft.AcceptedReviewDecisionId, draft.AcceptedReviewerMemberId,
            draft.PendingAuthorMemberIds.OrderBy(static memberId => memberId.ToString(),
                StringComparer.Ordinal).ToArray(), draftChangedAt,
            draft.GetResponsibilitySet(scope).ReadAssignments().ToArray());
    }

    internal static Result<IReadOnlyList<WorkCandidate>> Candidates(
        CommitmentDecisionWorkState state, DateTimeOffset now, Uuid? workItemId)
    {
        if (state.TenantId == Uuid.Empty || state.ProgramId == Uuid.Empty ||
            state.DraftId == Uuid.Empty || string.IsNullOrWhiteSpace(state.Identifier) ||
            state.SourceRevision < 1 || state.LatestEffectiveRevision is < 0 ||
            state.PendingAuthorMemberIds is null || state.Assignments is null)
            return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

        var kind = state.AcceptedReviewDecisionId is not null ? Approval :
            state.LatestEffectiveRevision != state.SourceRevision ? Review : null;
        if (kind is null)
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);

        var responsibilityType = kind == Approval
            ? ResponsibilityType.PolicyApprover
            : ResponsibilityType.AssignedReviewer;
        var nextAction = kind == Approval ? "approve" : "review";
        var route = kind == Approval ? "approvals" : "reviews";
        var reason = kind == Approval
            ? $"Commitment draft revision {state.SourceRevision} is awaiting approval after its accepted review."
            : $"Commitment draft revision {state.SourceRevision} is awaiting independent review.";
        var scope = new ResponsibilityScope(SeparationOfDutiesRecordTypes.Commitment,
            state.DraftId, state.DraftId, state.SourceRevision);
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
                (assignment.EffectiveUntil is not { } endsAt || now < endsAt) &&
                assignment.RevokedAt is null)
                activeAssignments.Add(assignment.MemberId);
        }

        var excluded = new HashSet<Uuid>(state.PendingAuthorMemberIds);
        if (kind == Approval && state.AcceptedReviewerMemberId is { } reviewer)
            excluded.Add(reviewer);

        var prefix = $"/api/v1/tenants/{state.TenantId}/programs/{state.ProgramId}/" +
                     $"commitment-drafts/{state.DraftId}";
        if (activeAssignments.Count > 0)
        {
            var candidates = activeAssignments.Where(memberId => !excluded.Contains(memberId))
                .OrderBy(static memberId => memberId.ToString(), StringComparer.Ordinal)
                .Select(memberId => CreateCandidate(state, kind, nextAction, route, reason,
                    prefix, workItemId,
                    new OperatingHolder(OperatingAuthority.MemberHolder, memberId), memberId))
                .Where(static candidate => candidate is not null)
                .Select(static candidate => candidate!)
                .ToArray();
            return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
        }

        var fallback = CreateCandidate(state, kind, nextAction, route, reason, prefix,
            workItemId,
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, state.ProgramId),
            null, excluded);
        return Result<IReadOnlyList<WorkCandidate>>.Success(fallback is null ? [] : [fallback]);
    }

    static WorkCandidate? CreateCandidate(CommitmentDecisionWorkState state, string kind,
        string nextAction, string route, string reason, string prefix,
        Uuid? wantedWorkItemId, OperatingHolder responsible, Uuid? assignedMemberId,
        IReadOnlySet<Uuid>? excluded = null)
    {
        var identity = Uuid.CreateVersion5(state.DraftId,
            $"commitment-decision\n{state.SourceRevision.ToString(CultureInfo.InvariantCulture)}\n" +
            $"{kind}\n{assignedMemberId?.ToString() ?? "program-reviewers"}");
        var id = WorkCandidate.IdFor(identity, kind);
        if (wantedWorkItemId is { } wanted && wanted != id)
            return null;
        return new WorkCandidate(id, kind, state.DraftId, null, null,
            $"{(nextAction == "review" ? "Review" : "Approve")} commitment {state.Identifier}",
            reason, null, null, nextAction, $"{prefix}/{route}", responsible, null,
            excluded ?? new HashSet<Uuid>(), state.DraftChangedAt)
        {
            RequiredManagementProgramId = state.ProgramId
        };
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The commitment work projection has an invalid program scope.");

    static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        "The commitment source and work projection differ. Retry the query.", isTransient: true);
}
