using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class AccessReviewCampaignWork
{
    public static IReadOnlyCollection<string> ProjectedKinds =>
        [WorkSource.AccessReviewReview, WorkSource.AccessReviewRemediation];

    public static bool IsReview(string kind) => kind == WorkSource.AccessReviewReview;

    public static bool IsRemediation(string kind) => kind == WorkSource.AccessReviewRemediation;

    public static IReadOnlyList<WorkCandidate> Candidates(AccessReviewCampaignWorkItemState state,
        DateTimeOffset now, Uuid? workItemId = null)
    {
        if (state.ProgramId == Uuid.Empty || state.IsCompleted ||
            state.SystemInstanceId == Uuid.Empty)
            return [];

        if (state.Decision is null)
        {
            if (state.CurrentReviewerMemberId is not { } reviewerMemberId ||
                reviewerMemberId == Uuid.Empty)
                return [];

            var candidate = CreateCandidate(state, reviewerMemberId,
                WorkSource.AccessReviewReview,
                    "record_decision", "decisions",
                    "Review a frozen access assignment",
                    "This frozen campaign item still needs a reviewer decision.");
            return workItemId is null || candidate.WorkItemId == workItemId
                ? [candidate]
                : [];
        }

        if (!AccessReviewVocabulary.RequiresRemediation(state.Decision) ||
            state.IsVerified ||
            state.HasException &&
            (state.ExceptionExpiresAt is null || now < state.ExceptionExpiresAt) ||
            state.CurrentRemediationOwnerMemberId is not { } ownerMemberId ||
            ownerMemberId == Uuid.Empty)
            return [];

        var remediationCandidate = state.HasProviderChange
            ? CreateCandidate(state, ownerMemberId, WorkSource.AccessReviewRemediation,
                "verify_remediation", "remediation-verifications",
                "Verify a provider-side remediation",
                "A provider change is recorded; accepted-population verification is still required.")
            : CreateCandidate(state, ownerMemberId, WorkSource.AccessReviewRemediation,
                "record_remediation_change", "remediation-changes",
                "Remediate a frozen access assignment",
                "The review decision requires provider-side remediation.");
        return workItemId is null || remediationCandidate.WorkItemId == workItemId
            ? [remediationCandidate]
            : [];
    }

    static WorkCandidate CreateCandidate(AccessReviewCampaignWorkItemState state,
        Uuid responsibleMemberId, string kind, string nextAction,
        string endpoint, string summary, string reason)
    {
        HashSet<Uuid> excluded = [];
        if (IsReview(kind) && state.SubjectMemberId is { } subjectMemberId)
            excluded.Add(subjectMemberId);
        var requiresWaiver = IsReview(kind) && excluded.Contains(responsibleMemberId);
        var workItemId = WorkItemIdFor(state.TenantId, state.ProgramId, state.CampaignId,
            state.ItemId, kind);
        return new WorkCandidate(workItemId, kind, state.ItemId, null, null, summary, reason,
            state.DueOn, state.Privileged ? "high" : null, nextAction,
            $"/api/v1/tenants/{state.TenantId}/access-review-campaigns/{state.CampaignId}/items/{state.ItemId}/{endpoint}",
            new OperatingHolder(OperatingAuthority.MemberHolder, responsibleMemberId), null,
            excluded, state.LaunchedAt)
        {
            ProgramId = state.ProgramId,
            RestrictedSystemInstanceId = state.SystemInstanceId,
            RequiresSeparationOfDutiesWaiver = requiresWaiver,
        };
    }

    public static Uuid WorkItemIdFor(Uuid tenantId, Uuid programId, Uuid campaignId,
        Uuid itemId, string kind) => Uuid.CreateVersion5(tenantId,
        $"{programId}\n{campaignId}\n{itemId}\n{kind}\nv1");
}
