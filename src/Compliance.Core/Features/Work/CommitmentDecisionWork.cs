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
                    draft.Revision != summary.Revision)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(SourceChanged());

                candidates.AddRange(Candidates(draft, summary, tenantId, programId, now,
                    workItemId));
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

    static List<WorkCandidate> Candidates(CommitmentDraft draft, CommitmentDraftView summary,
        Uuid tenantId, Uuid programId, DateTimeOffset now, Uuid? workItemId)
    {
        string kind;
        ResponsibilityType responsibilityType;
        string nextAction;
        string route;
        string reason;
        if (draft.AcceptedReviewDecisionId is not null)
        {
            kind = Approval;
            responsibilityType = ResponsibilityType.PolicyApprover;
            nextAction = "approve";
            route = "approvals";
            reason = $"Commitment draft revision {draft.Revision} is awaiting approval after its accepted review.";
        }
        else if (draft.LatestEffectiveRevision != draft.Revision)
        {
            kind = Review;
            responsibilityType = ResponsibilityType.AssignedReviewer;
            nextAction = "review";
            route = "reviews";
            reason = $"Commitment draft revision {draft.Revision} is awaiting independent review.";
        }
        else
            return [];

        var scope = new ResponsibilityScope(SeparationOfDutiesRecordTypes.Commitment,
            draft.Id, draft.Id, draft.Revision);
        var activeAssignments = draft.GetResponsibilitySet(scope).ReadAssignments()
            .Where(assignment => assignment.TenantId == tenantId && assignment.Scope == scope &&
                assignment.Type == responsibilityType && assignment.EffectiveFrom <= now &&
                (assignment.EffectiveUntil is null || now < assignment.EffectiveUntil) &&
                assignment.RevokedAt is null)
            .Select(static assignment => assignment.MemberId)
            .Distinct()
            .OrderBy(static memberId => memberId.ToString(), StringComparer.Ordinal)
            .ToArray();

        var excluded = new HashSet<Uuid>(draft.PendingAuthorMemberIds);
        if (kind == Approval && draft.AcceptedReviewerMemberId is { } reviewer)
            excluded.Add(reviewer);

        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}/" +
                     $"commitment-drafts/{draft.Id}";
        var eligibleAssignments = activeAssignments.Where(memberId => !excluded.Contains(memberId))
            .ToArray();
        if (activeAssignments.Length > 0)
            return eligibleAssignments.Select(memberId => CreateCandidate(draft, summary,
                kind, nextAction, route, reason, prefix, workItemId,
                new OperatingHolder(OperatingAuthority.MemberHolder, memberId), memberId))
                .Where(static candidate => candidate is not null)
                .Select(static candidate => candidate!)
                .ToList();

        var fallback = CreateCandidate(draft, summary, kind, nextAction, route, reason, prefix,
            workItemId, new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId),
            null, excluded);
        return fallback is null ? [] : [fallback];
    }

    static WorkCandidate? CreateCandidate(CommitmentDraft draft, CommitmentDraftView summary,
        string kind, string nextAction, string route, string reason, string prefix,
        Uuid? wantedWorkItemId, OperatingHolder responsible, Uuid? assignedMemberId,
        IReadOnlySet<Uuid>? excluded = null)
    {
        var identity = Uuid.CreateVersion5(draft.Id,
            $"commitment-decision\n{draft.Revision.ToString(CultureInfo.InvariantCulture)}\n" +
            $"{kind}\n{assignedMemberId?.ToString() ?? "program-reviewers"}");
        var workItemId = WorkCandidate.IdFor(identity, kind);
        if (wantedWorkItemId is { } wanted && wanted != workItemId)
            return null;
        return new WorkCandidate(workItemId, kind, draft.Id, null, null,
            $"{(nextAction == "review" ? "Review" : "Approve")} commitment {summary.Identifier}",
            reason, null, null, nextAction, $"{prefix}/{route}", responsible, null,
            excluded ?? new HashSet<Uuid>(), summary.LastChangedAt);
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The commitment work projection has an invalid program scope.");

    static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        "The commitment source and work projection differ. Retry the query.", isTransient: true);
}
