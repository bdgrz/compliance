using System.Globalization;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Derives policy decision work from the current policy stream and list projection.</summary>
static class PolicyDecisionWork
{
    public const string DraftReview = "policy_draft_review";
    public const string DraftApproval = "policy_draft_approval";
    public const string RetirementReview = "policy_retirement_review";
    public const string RetirementApproval = "policy_retirement_approval";
    public const string PeriodicReview = "policy_periodic_review";

    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader, IPolicyDirectoryReader directory,
        PolicyDirectoryReadConsistency? consistency, Uuid tenantId, Uuid programId,
        DateOnly today, DateOnly horizon, Uuid? workItemId, CancellationToken ct)
    {
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
                var policy = await reader.HydrateAsync(new Policy(tenantId, summary.PolicyId), ct)
                    .ConfigureAwait(false);
                if (!policy.IsVisible || policy.ProgramId != programId)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(SourceChanged());

                var view = policy.ToView(today);
                if (!Matches(summary, view))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(SourceChanged());

                candidates.AddRange(Candidates(policy, view, tenantId, programId, horizon,
                    workItemId));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        if (consistency is not null)
        {
            var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
                fence, ct).ConfigureAwait(false);
            if (!confirmed.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(confirmed.Error);
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    static List<WorkCandidate> Candidates(Policy policy, PolicyView view, Uuid tenantId,
        Uuid programId, DateOnly horizon, Uuid? workItemId)
    {
        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}/policies/{policy.Id}";
        var title = policy.DraftContent?.Title ?? policy.CurrentVersion?.Content.Title ??
                    policy.Identifier ?? "policy";
        var holder = new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId);
        var candidates = new List<WorkCandidate>();

        void Add(string kind, string summary, string reason, DateOnly? dueOn, string nextAction,
            string route, IReadOnlySet<Uuid> excluded, long? version = null,
            string? identitySuffix = null)
        {
            var identity = Uuid.CreateVersion5(policy.Id,
                $"policy-decision\n{policy.Revision}\n" +
                $"{version?.ToString(CultureInfo.InvariantCulture) ?? "-"}\n{kind}\n" +
                identitySuffix);
            var candidate = new WorkCandidate(WorkCandidate.IdFor(identity, kind), kind,
                policy.Id, null, null, summary, reason, dueOn, null, nextAction,
                $"{prefix}/{route}", holder, null, excluded, view.LastChangedAt);
            if (workItemId is not { } wanted || candidate.WorkItemId == wanted)
                candidates.Add(candidate);
        }

        var authors = policy.PendingAuthorMemberIds;
        switch (view.PendingStatus)
        {
            case "draft":
                Add(DraftReview, $"Review {policy.Identifier} draft: {title}",
                    $"Policy draft revision {view.Revision} is awaiting independent review.",
                    null, "review", "reviews", authors);
                break;
            case "awaiting_approval":
                Add(DraftApproval, $"Approve {policy.Identifier} draft: {title}",
                    $"Policy draft revision {view.Revision} is awaiting approval after its accepted review.",
                    null, "approve", "approvals", ApprovalExclusions(policy),
                    policy.CurrentVersion?.Version);
                break;
            case "retirement_proposed":
                Add(RetirementReview, $"Review retirement of {policy.Identifier}",
                    $"Retirement of policy version {view.PendingRetirement?.Version} at revision " +
                    $"{view.Revision} is awaiting independent review.", null, "review", "reviews",
                    authors, view.PendingRetirement?.Version);
                break;
            case "retirement_awaiting_approval":
                Add(RetirementApproval, $"Approve retirement of {policy.Identifier}",
                    $"Retirement of policy version {view.PendingRetirement?.Version} at revision " +
                    $"{view.Revision} is awaiting approval after its accepted review.", null,
                    "approve", "retirements", ApprovalExclusions(policy),
                    view.PendingRetirement?.Version);
                break;
        }

        if (view.Status == "approved" && view.CurrentVersion is { } current &&
            view.NextReviewDueOn is { } dueOn && dueOn <= horizon)
            Add(PeriodicReview, $"Review {policy.Identifier} version {current}",
                $"Approved policy version {current} is due for periodic review.", dueOn,
                "confirm_review", "periodic-reviews", new HashSet<Uuid>(), current,
                dueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return candidates;
    }

    static HashSet<Uuid> ApprovalExclusions(Policy policy)
    {
        var excluded = new HashSet<Uuid>(policy.PendingAuthorMemberIds);
        if (policy.AcceptedReviewerMemberId is { } reviewer)
            excluded.Add(reviewer);
        return excluded;
    }

    static bool Matches(PolicySummaryView summary, PolicyView source) =>
        summary.TenantId == source.TenantId && summary.ProgramId == source.ProgramId &&
        summary.PolicyId == source.PolicyId && summary.Identifier == source.Identifier &&
        summary.Status == source.Status && summary.PendingStatus == source.PendingStatus &&
        summary.Revision == source.Revision && summary.CurrentVersion == source.CurrentVersion &&
        summary.CurrentEffectiveFrom == source.CurrentEffectiveFrom &&
        summary.NextReviewDueOn == source.NextReviewDueOn &&
        summary.LastChangedAt == source.LastChangedAt;

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The policy work projection has an invalid program scope.");

    static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        "The policy source and work projection differ. Retry the query.", isTransient: true);
}
