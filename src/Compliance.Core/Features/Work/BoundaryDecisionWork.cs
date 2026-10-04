using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Derives assigned boundary review and approval work from current draft decisions.</summary>
static class BoundaryDecisionWork
{
    public const string Review = "boundary_review";
    public const string Approval = "boundary_approval";

    public static async ValueTask<IReadOnlyList<WorkCandidate>> LoadAsync(IAggregateReader reader,
        IBoundaryDirectoryReader directory, Uuid tenantId, Uuid programId, DateTimeOffset now,
        Uuid? workItemId, CancellationToken ct)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(tenantId, programId, 200, cursor, ct)
                .ConfigureAwait(false);
            foreach (var boundary in page.Items.Where(boundary => boundary.TenantId == tenantId &&
                         boundary.ProgramId == programId && boundary.Draft is not null))
            {
                var draft = boundary.Draft!;
                if (draft.TenantId != tenantId || draft.BoundaryId != boundary.BoundaryId ||
                    draft.ProgramId != programId || draft.Status != "draft")
                    continue;
                var aggregate = await reader.HydrateAsync(new SystemBoundary(tenantId,
                    boundary.BoundaryId), ct).ConfigureAwait(false);
                if (!aggregate.IsCreated || aggregate.ProgramId != programId ||
                    aggregate.DraftVersionId != draft.VersionId ||
                    aggregate.DraftRevision != draft.Revision)
                    continue;

                var decision = boundary.LatestDecision;
                if (decision is not null && (decision.TenantId != tenantId ||
                    decision.BoundaryId != boundary.BoundaryId || decision.VersionId != draft.VersionId ||
                    decision.Revision != draft.Revision))
                    continue;
                string kind;
                ResponsibilityType responsibilityType;
                string nextAction;
                string route;
                if (decision is null)
                {
                    kind = Review;
                    responsibilityType = ResponsibilityType.AssignedReviewer;
                    nextAction = "review";
                    route = "reviews";
                }
                else if (decision.Outcome == "accept")
                {
                    kind = Approval;
                    responsibilityType = ResponsibilityType.PolicyApprover;
                    nextAction = "approve";
                    route = "approvals";
                }
                else
                    continue;

                var scope = new ResponsibilityScope("boundary", boundary.BoundaryId,
                    draft.VersionId, draft.Revision);
                foreach (var assignment in aggregate.GetResponsibilitySet(scope).ReadAssignments()
                             .Where(assignment => assignment.TenantId == tenantId &&
                                 assignment.Scope == scope && assignment.Type == responsibilityType &&
                                 assignment.EffectiveFrom <= now &&
                                 (assignment.EffectiveUntil is null || now < assignment.EffectiveUntil) &&
                                 (assignment.RevokedAt is null || now < assignment.RevokedAt) &&
                                 assignment.MemberId != draft.AuthorMemberId))
                {
                    var identity = Uuid.CreateVersion5(draft.VersionId,
                        $"{kind}\n{draft.Revision}\n{assignment.MemberId}");
                    var id = WorkCandidate.IdFor(identity, kind);
                    if (workItemId is { } wanted && id != wanted)
                        continue;
                    var path = $"/api/v1/tenants/{tenantId}/boundaries/{boundary.BoundaryId}/" +
                               $"drafts/{draft.VersionId}/{route}";
                    candidates.Add(new WorkCandidate(id, kind, draft.VersionId, null, null,
                        nextAction == "review" ? "Review system boundary" : "Approve system boundary",
                        $"Boundary draft revision {draft.Revision} is awaiting an assigned {nextAction}.",
                        null, null, nextAction, path,
                        new OperatingHolder(OperatingAuthority.MemberHolder, assignment.MemberId), null,
                        new HashSet<Uuid>(), draft.ChangedAt));
                }
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return candidates;
    }
}
