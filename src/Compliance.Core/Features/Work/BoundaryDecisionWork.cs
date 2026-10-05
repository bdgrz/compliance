using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Derives assigned boundary review and approval work from current draft decisions.</summary>
static class BoundaryDecisionWork
{
    public const string Review = "boundary_review";
    public const string Approval = "boundary_approval";
    public static IReadOnlyList<string> ProjectedKinds { get; } = Array.AsReadOnly<string>(
        [Review, Approval]);

    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader, IBoundaryDirectoryReader directory, Uuid tenantId,
        Uuid programId, DateTimeOffset now, Uuid? workItemId, CancellationToken ct)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(tenantId, programId, 200, cursor, ct)
                .ConfigureAwait(false);
            foreach (var boundary in page.Items.Where(boundary => boundary.TenantId == tenantId &&
                         boundary.ProgramId == programId))
            {
                var aggregate = await reader.HydrateAsync(new SystemBoundary(tenantId,
                    boundary.BoundaryId), ct).ConfigureAwait(false);
                if (!aggregate.IsCreated || aggregate.ProgramId != programId)
                    continue;
                if (aggregate.Revision != boundary.Revision)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The boundary source and work projection revisions differ. Retry the query.",
                        isTransient: true));

                var draft = boundary.Draft;
                if (draft is null || draft.TenantId != tenantId ||
                    draft.BoundaryId != boundary.BoundaryId || draft.ProgramId != programId ||
                    draft.Status != "draft" || aggregate.DraftVersionId != draft.VersionId ||
                    aggregate.DraftRevision != draft.Revision)
                    continue;

                var decision = boundary.LatestDecision;
                if (decision is not null && (decision.TenantId != tenantId ||
                    decision.BoundaryId != boundary.BoundaryId || decision.VersionId != draft.VersionId ||
                    decision.Revision != draft.Revision))
                    continue;
                var scope = new ResponsibilityScope("boundary", boundary.BoundaryId,
                    draft.VersionId, draft.Revision);
                var state = new BoundaryDecisionWorkState(tenantId, programId,
                    boundary.BoundaryId, aggregate.Revision, draft.VersionId, draft.Revision,
                    draft.AuthorMemberId, draft.ChangedAt, decision?.Outcome,
                    aggregate.GetResponsibilitySet(scope).ReadAssignments().ToArray());
                var work = Candidates(state, now, workItemId);
                if (!work.IsSuccess)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(work.Error);
                candidates.AddRange(work.Value);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    internal static Result<IReadOnlyList<WorkCandidate>> Candidates(
        BoundaryDecisionWorkState state, DateTimeOffset now, Uuid? workItemId)
    {
        if (state.TenantId == Uuid.Empty || state.ProgramId == Uuid.Empty ||
            state.BoundaryId == Uuid.Empty || state.SourceRevision < 1 ||
            state.DraftVersionId == Uuid.Empty || state.DraftRevision < 1 ||
            state.DraftAuthorMemberId == Uuid.Empty ||
            state.Assignments is null || state.LatestReviewOutcome is not
                (null or "accept" or "request_changes"))
            return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

        if (state.LatestReviewOutcome == "request_changes")
            return Result<IReadOnlyList<WorkCandidate>>.Success([]);

        var kind = state.LatestReviewOutcome == "accept" ? Approval : Review;
        var responsibilityType = state.LatestReviewOutcome == "accept"
            ? ResponsibilityType.PolicyApprover
            : ResponsibilityType.AssignedReviewer;
        var nextAction = state.LatestReviewOutcome == "accept" ? "approve" : "review";
        var route = state.LatestReviewOutcome == "accept" ? "approvals" : "reviews";
        var scope = new ResponsibilityScope("boundary", state.BoundaryId,
            state.DraftVersionId, state.DraftRevision);
        var assignments = new HashSet<Uuid>();
        var workItemIds = new HashSet<Uuid>();
        var candidates = new List<WorkCandidate>();
        var prefix = $"/api/v1/tenants/{state.TenantId}/boundaries/{state.BoundaryId}/" +
                     $"drafts/{state.DraftVersionId}/{route}";

        foreach (var assignment in state.Assignments)
        {
            if (assignment is null || assignment.TenantId != state.TenantId ||
                assignment.AssignmentId == Uuid.Empty || assignment.MemberId == Uuid.Empty ||
                assignment.Scope != scope || !Enum.IsDefined(assignment.Type) ||
                assignment.EffectiveUntil is { } until && until <= assignment.EffectiveFrom ||
                !assignments.Add(assignment.AssignmentId))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

            if (assignment.Type != responsibilityType ||
                assignment.EffectiveFrom > now ||
                assignment.EffectiveUntil is { } endsAt && now >= endsAt ||
                assignment.RevokedAt is { } revokedAt && now >= revokedAt ||
                assignment.MemberId == state.DraftAuthorMemberId)
                continue;

            var identity = Uuid.CreateVersion5(state.DraftVersionId,
                $"{kind}\n{state.DraftRevision}\n{assignment.MemberId}");
            var id = WorkCandidate.IdFor(identity, kind);
            if ((workItemId is { } wanted && id != wanted) || !workItemIds.Add(id))
                continue;
            candidates.Add(new WorkCandidate(id, kind, state.DraftVersionId, null, null,
                nextAction == "review" ? "Review system boundary" : "Approve system boundary",
                $"Boundary draft revision {state.DraftRevision} is awaiting an assigned {nextAction}.",
                null, null, nextAction, prefix,
                new OperatingHolder(OperatingAuthority.MemberHolder, assignment.MemberId), null,
                new HashSet<Uuid>(), state.DraftChangedAt));
        }

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The boundary work projection has an invalid program scope.");
}
