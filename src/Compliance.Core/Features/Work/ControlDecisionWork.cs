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

                candidates.AddRange(await CandidatesAsync(control, summary.Identifier, authority,
                    tenantId, programId, now, workItemId, lifecycleEnabled, ct)
                    .ConfigureAwait(false));
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

    static async ValueTask<List<WorkCandidate>> CandidatesAsync(ControlDraft control,
        string identifier, OperatingAuthority authority, Uuid tenantId, Uuid programId,
        DateTimeOffset now, Uuid? wantedWorkItemId, bool lifecycleEnabled, CancellationToken ct)
    {
        var isRetirement = control.PendingRetirementId is not null;
        if (!lifecycleEnabled && (isRetirement || control.IsApproved))
            return [];

        var targetId = control.PendingTargetId;
        var changedAt = control.PendingDecisionChangedAt;
        if (targetId is null || changedAt is null || !isRetirement && !control.HasOpenDraft)
            return [];

        var approval = control.AcceptedReviewDecisionId is not null;
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
        var scope = new ResponsibilityScope("control", control.Id, targetId.Value,
            control.Revision);
        var activeAssignments = control.GetResponsibilitySet(scope).ReadAssignments()
            .Where(assignment => assignment.TenantId == tenantId && assignment.Scope == scope &&
                assignment.Type == responsibilityType && assignment.EffectiveFrom <= now &&
                (assignment.EffectiveUntil is null || now < assignment.EffectiveUntil) &&
                (assignment.RevokedAt is null || now < assignment.RevokedAt))
            .Select(static assignment => assignment.MemberId)
            .Distinct()
            .OrderBy(static memberId => memberId.ToString(), StringComparer.Ordinal)
            .ToArray();

        var excluded = new HashSet<Uuid>();
        if (control.PendingAuthorMemberId is { } author)
            excluded.Add(author);
        if (approval && control.AcceptedReviewerMemberId is { } reviewer)
            excluded.Add(reviewer);

        var responsibleMembers = new List<Uuid>();
        foreach (var memberId in activeAssignments.Where(memberId => !excluded.Contains(memberId)))
            if (await authority.HasProgramManagementPermissionAsync(tenantId, programId, memberId,
                    ct).ConfigureAwait(false))
                responsibleMembers.Add(memberId);
        if (responsibleMembers.Count == 0)
            return CreateCandidates(control, identifier, tenantId, programId, targetId.Value,
                changedAt.Value, kind, approval, isRetirement, wantedWorkItemId,
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                excluded);

        return responsibleMembers.Select(memberId => CreateCandidates(control, identifier,
                tenantId, programId, targetId.Value, changedAt.Value, kind, approval,
                isRetirement, wantedWorkItemId,
                new OperatingHolder(OperatingAuthority.MemberHolder, memberId), memberId, excluded))
            .SelectMany(static candidates => candidates)
            .ToList();
    }

    static List<WorkCandidate> CreateCandidates(ControlDraft control, string identifier,
        Uuid tenantId, Uuid programId, Uuid targetId, DateTimeOffset changedAt, string kind,
        bool approval, bool isRetirement, Uuid? wantedWorkItemId,
        OperatingHolder responsible, Uuid? assignedMemberId, IReadOnlySet<Uuid> excluded)
    {
        var round = approval
            ? control.AcceptedReviewDecisionId?.ToString()
            : control.LatestReviewDecisionId?.ToString() ?? "initial";
        var identity = Uuid.CreateVersion5(targetId,
            $"control-decision\n{control.Revision.ToString(CultureInfo.InvariantCulture)}\n" +
            $"{kind}\n{round}\n{assignedMemberId?.ToString() ?? "program-reviewers"}");
        var workItemId = WorkCandidate.IdFor(identity, kind);
        if (wantedWorkItemId is { } wanted && wanted != workItemId)
            return [];

        var verb = approval ? "Approve" : "Review";
        var summary = isRetirement
            ? $"{verb} retirement of {identifier}"
            : $"{verb} {identifier} control draft";
        var route = approval && isRetirement
            ? "retirements"
            : $"draft/{(approval ? "approvals" : "reviews")}";
        var reason = isRetirement
            ? $"Control retirement revision {control.Revision} is awaiting an independent " +
              $"{(approval ? "approval" : "review")}."
            : $"Control draft revision {control.Revision} is awaiting an independent " +
              $"{(approval ? "approval" : "review")}" +
              (approval ? " after its accepted review." : ".");
        return
        [
            new WorkCandidate(workItemId, kind, targetId, control.Id, null, summary, reason,
                null, null, approval ? "approve" : "review",
                $"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{control.Id}/{route}",
                responsible, null, excluded, changedAt),
        ];
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control work projection has an invalid program scope.");

    static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        "The control work source and directory differ. Retry the query.", isTransient: true);
}
