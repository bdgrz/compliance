using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Replaces an ineligible frozen campaign owner with an attributable source event.</summary>
public sealed class ReassignAccessReviewResponsibilityHandler(IAggregateReader reader,
    IAggregateExecutor executor, TimeProvider clock, AccessReviewQueueEligibility eligibility,
    OperatingAuthority authority, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ReassignAccessReviewResponsibility,
        AccessReviewResponsibilityReassignmentView>
{
    public async ValueTask<Result<AccessReviewResponsibilityReassignmentView>> HandleAsync(
        IRequestContext<ReassignAccessReviewResponsibility> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        var launched = campaign.Launched;
        var item = campaign.FindItem(request.ItemId);
        if (launched?.ProgramId is not { } programId || launched.RemediationOwnerMemberId is null ||
            item is null ||
            !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                item.SystemInstanceId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.NotFound, "The review item was not found.");
        if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, programId,
                actor.MemberId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden,
                "The actor must currently manage the campaign's owner Program.");

        if (!AccessReviewResponsibilityKind.IsKnown(request.Responsibility) ||
            request.AssignedMemberId == Uuid.Empty ||
            !AccessReviewVocabulary.IsBoundedText(request.Reason, 2000))
            return Failure(RequestErrorKind.Validation,
                "A reassignment requires a known responsibility, member, and reason.");
        if (request.Responsibility == AccessReviewResponsibilityKind.Reviewer &&
            !AccessReviewVocabulary.IsBoundedText(request.DelegationReason, 2000))
            return Failure(RequestErrorKind.Validation,
                "A reviewer replacement is a delegate and requires a delegation reason.");
        if (request.Responsibility == AccessReviewResponsibilityKind.RemediationOwner &&
            request.DelegationReason is not null)
            return Failure(RequestErrorKind.Validation,
                "A remediation-owner reassignment cannot include a reviewer delegation reason.");
        if (campaign.FindResponsibilityReassignment(context.RequestId) is { } existing)
            return campaign.FindResponsibilityReassignmentExpectedRevision(context.RequestId) ==
                   request.ExpectedRevision &&
                   existing.Responsibility == request.Responsibility &&
                   existing.ItemId == request.ItemId &&
                   existing.AssignedMemberId == request.AssignedMemberId &&
                   existing.Reason == request.Reason.Trim() && existing.ReassignedBy == actor.Reference &&
                   existing.DelegationReason == request.DelegationReason?.Trim()
                ? Result<AccessReviewResponsibilityReassignmentView>.Success(existing)
                : Failure(RequestErrorKind.Conflict,
                    "The reassignment request ID already has different content.");

        var previousMemberId = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
            ? campaign.CurrentReviewerMemberId(request.ItemId)
            : campaign.CurrentRemediationOwnerMemberId(request.ItemId);
        if (previousMemberId is not { } previous || previous == Uuid.Empty)
            return Failure(RequestErrorKind.NotFound, "The responsibility was not found.");
        var previousEligible = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
            ? await eligibility.CanReviewAsync(request.TenantId, programId, previous,
                item.SystemInstanceId, ct)
                .ConfigureAwait(false)
            : await eligibility.CanRemediateAsync(request.TenantId, programId, previous,
                item.SystemInstanceId, ct)
                .ConfigureAwait(false);
        if (previousEligible)
            return Failure(RequestErrorKind.Conflict,
                "A responsibility can be reassigned only after its current owner loses eligibility.");

        var distinctMemberId = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
            ? campaign.CurrentRemediationOwnerMemberId(request.ItemId)
            : campaign.CurrentReviewerMemberId(request.ItemId);
        if (request.AssignedMemberId == distinctMemberId)
            return Failure(RequestErrorKind.Validation,
                "The reviewer and remediation owner must remain distinct.");
        var assignedEligible = request.Responsibility == AccessReviewResponsibilityKind.Reviewer
            ? await eligibility.CanReviewAsync(request.TenantId, programId, request.AssignedMemberId,
                item.SystemInstanceId, ct).ConfigureAwait(false)
            : await eligibility.CanRemediateAsync(request.TenantId, programId,
                request.AssignedMemberId,
                item.SystemInstanceId, ct).ConfigureAwait(false);
        if (!assignedEligible)
            return Failure(RequestErrorKind.Validation,
                "The replacement must have current queue-read, access-review, membership, and restricted-system eligibility for this responsibility.");

        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId), aggregate => AccessReviewOutcome.From(
                aggregate.ReassignResponsibility(request.ItemId, request.Responsibility,
                    request.ExpectedRevision, context.RequestId, request.AssignedMemberId,
                    request.Reason, actor.Reference, clock.GetUtcNow(), request.DelegationReason)), context, ct)
            .ConfigureAwait(false);
    }

    static Result<AccessReviewResponsibilityReassignmentView> Failure(RequestErrorKind kind,
        string message) => AccessReviewOutcome.Failure<AccessReviewResponsibilityReassignmentView>(kind,
        message);
}
