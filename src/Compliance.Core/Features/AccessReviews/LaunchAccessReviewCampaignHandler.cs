using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Builds one item per effective access row of each assigned accepted population, with the
///     classification and variance known now, freezes items, reviewers, instructions, and deadline
///     as an immutable snapshot, and records the launch. Later classification or expectation
///     changes never alter a launched campaign.
/// </summary>
public sealed class LaunchAccessReviewCampaignHandler(IAggregateReader reader,
    IAggregateExecutor executor, PopulationSnapshotFreezer freezer, IAccessReviewSources sources,
    TimeProvider clock, RestrictedApplicationVisibility visibility,
    AccessReviewQueueEligibility queueEligibility, OperatingAuthority authority)
    : IRequestHandler<LaunchAccessReviewCampaign, AccessReviewCampaignRegistration>
{
    public async ValueTask<Result<AccessReviewCampaignRegistration>> HandleAsync(
        IRequestContext<LaunchAccessReviewCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaignId = context.RequestId;
        var existing = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId, campaignId),
            ct).ConfigureAwait(false);
        if (existing.Launched is { } launched)
        {
            var existingActor = AccessReviewActor.From(context);
            if (!await RestrictedAccessReviewVisibility.CanReadCampaignItemsAsync(visibility,
                    request.TenantId, existingActor.UserId, existing, ct: ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.NotFound, "The campaign was not found.");
            if (launched.ProgramId is not { } ownerProgramId || ownerProgramId == Uuid.Empty)
                return Failure(RequestErrorKind.Conflict,
                    "The campaign already exists without a routed owner Program.");
            var ownerProgram = await reader.HydrateAsync(
                new ComplianceProgram(request.TenantId, ownerProgramId), ct).ConfigureAwait(false);
            if (!ownerProgram.IsCreated)
                return Failure(RequestErrorKind.NotFound, "The owner Program was not found in this tenant.");
            if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, ownerProgramId,
                    existingActor.MemberId, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Forbidden,
                    "The launching actor must currently manage the owner Program.");
            if (!existing.MatchesLaunchRequest(request))
                return Failure(RequestErrorKind.Conflict,
                    "The campaign already exists with different frozen launch content.");
            return Result<AccessReviewCampaignRegistration>.Success(new(campaignId,
                launched.SnapshotId, launched.ContentSha256, launched.Items.Count));
        }
        var now = clock.GetUtcNow();
        if (request.ProgramId is not { } programId || programId == Uuid.Empty ||
            request.RemediationOwnerMemberId is not { } remediationOwnerMemberId ||
            remediationOwnerMemberId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation,
                "A routed campaign requires an explicit owner Program and remediation owner.");
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, programId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound,
                "The owner Program was not found in this tenant.");
        var actor = AccessReviewActor.From(context);
        if (!await authority.HasProgramManagementPermissionAsync(request.TenantId, programId,
                actor.MemberId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden,
                "The launching actor must currently manage the owner Program.");
        if (request.Assignments is not { Count: > 0 } ||
            request.Assignments.Select(static assignment => assignment.PopulationId).Distinct().Count() !=
            request.Assignments.Count)
            return Failure(RequestErrorKind.Validation,
                "A campaign assigns one or more distinct accepted populations.");
        if (!AccessReviewVocabulary.IsBoundedText(request.Name, 200) ||
            !AccessReviewVocabulary.IsBoundedText(request.Instructions, 8000) || request.Deadline <= now)
            return Failure(RequestErrorKind.Validation,
                "A campaign requires a name, instructions, and a future deadline.");

        var reviewers = new List<AccessReviewerView>();
        var items = new List<AccessReviewItemView>();
        var correlated = new Dictionary<Uuid, Uuid?>();
        foreach (var assignment in request.Assignments)
        {
            if (assignment.ReviewerMemberId == Uuid.Empty)
                return Failure(RequestErrorKind.Validation, "Each assignment names a reviewer.");
            var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
                assignment.PopulationId), ct).ConfigureAwait(false);
            if (population.Opened is not { } opened ||
                !await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                    opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.NotFound, "The population was not found.");
            var loaded = await AcceptedAccessPopulation.LoadAsync(reader, request.TenantId,
                assignment.PopulationId, ct).ConfigureAwait(false);
            if (!loaded.IsSuccess)
                return Result<AccessReviewCampaignRegistration>.Failure(loaded.Error);
            var accepted = loaded.Value;
            if (!await queueEligibility.CanRemediateAsync(request.TenantId, programId,
                    remediationOwnerMemberId, accepted.Header.SystemInstanceId, ct)
                    .ConfigureAwait(false))
                return Failure(RequestErrorKind.Validation,
                    "The remediation owner must be active, have program-queue read and access-review management authority, and see every restricted system.");
            if (assignment.ReviewerMemberId == remediationOwnerMemberId)
                return Failure(RequestErrorKind.Validation,
                    "The remediation owner must be distinct from every frozen reviewer.");
            if (!await queueEligibility.CanReviewAsync(request.TenantId, programId,
                    assignment.ReviewerMemberId, accepted.Header.SystemInstanceId, ct)
                    .ConfigureAwait(false))
                return Failure(RequestErrorKind.Validation,
                    "Each reviewer must be an active tenant member who can read the program queue and every assigned restricted system.");
            var owner = await sources.AccessOwnerMemberAsync(request.TenantId,
                accepted.Header.ApplicationId, ct).ConfigureAwait(false);
            if (!owner.IsSuccess)
                return Result<AccessReviewCampaignRegistration>.Failure(owner.Error);
            var delegated = owner.Value != assignment.ReviewerMemberId;
            if (delegated && !AccessReviewVocabulary.IsBoundedText(assignment.DelegationReason, 2000))
                return Failure(RequestErrorKind.Validation,
                    "A reviewer other than the application's access owner is a delegate and requires a delegation reason.");
            reviewers.Add(new AccessReviewerView(accepted.Population.Id, accepted.Header.SystemInstanceId,
                assignment.ReviewerMemberId, delegated, delegated ? assignment.DelegationReason!.Trim() : null));

            var variance = await GetAccessVarianceHandler.EvaluateAsync(reader, request.TenantId,
                accepted, ct).ConfigureAwait(false);
            var principals = accepted.Facts.Principals.ToDictionary(static principal =>
                principal.ProviderSubjectId, StringComparer.Ordinal);
            var entitlements = accepted.Facts.Entitlements.ToDictionary(static entitlement =>
                entitlement.ProviderEntitlementId, StringComparer.Ordinal);
            foreach (var row in accepted.EffectiveAccess)
            {
                var principal = principals[row.ProviderSubjectId];
                var entitlement = entitlements[row.ProviderEntitlementId];
                var classification = accepted.Population.CurrentClassification(row.ProviderSubjectId);
                var personId = classification?.Classification == AccessReviewVocabulary.Human
                    ? classification.PersonId
                    : null;
                Uuid? userId = null;
                if (personId is { } id)
                {
                    if (!correlated.TryGetValue(id, out userId))
                        correlated[id] = userId = (await reader.HydrateAsync(
                            new Person(request.TenantId, id), ct).ConfigureAwait(false)).CorrelatedUserId;
                }
                var explained = variance.FirstOrDefault(item =>
                    item.ProviderSubjectId == row.ProviderSubjectId &&
                    item.ProviderEntitlementId == row.ProviderEntitlementId);
                items.Add(new AccessReviewItemView(ItemId(campaignId, accepted.Population.Id, row),
                    accepted.Population.Id, accepted.Snapshot.Id, accepted.Header.SystemInstanceId,
                    assignment.ReviewerMemberId, row.ProviderSubjectId, principal.PrincipalKind,
                    principal.DisplayName, classification?.Classification ?? AccessReviewVocabulary.Unclassified,
                    personId, userId, row.ProviderEntitlementId, entitlement.EntitlementKind,
                    entitlement.DisplayName, explained?.Privileged == true,
                    explained?.Category ?? AccessVarianceEvaluator.Unexpected, row.Paths, row.ExpiresAt));
            }
        }
        if (items.Count == 0)
            return Failure(RequestErrorKind.Validation,
                "The assigned populations have no effective access to review.");

        var header = new AccessReviewCampaignHeader(campaignId, request.Name.Trim(),
            request.Instructions.Trim(), request.Deadline, programId, remediationOwnerMemberId);
        var frozen = await freezer.FreezeAsync(context, request.TenantId,
            AccessReviewCampaignContent.LaunchKind,
            AccessReviewCampaignContent.LaunchRows(header, reviewers, items), null, null,
            actor.Reference, ct).ConfigureAwait(false);
        if (!frozen.IsSuccess)
            return Result<AccessReviewCampaignRegistration>.Failure(frozen.Error);
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId, campaignId),
            campaign => AccessReviewOutcome.From(campaign.Launch(request.Name, request.Instructions,
                request.Deadline, frozen.Value.SnapshotId, frozen.Value.ContentSha256, reviewers,
                items, actor.Reference, now, programId, remediationOwnerMemberId)), context, ct)
            .ConfigureAwait(false);
    }

    static Uuid ItemId(Uuid campaignId, Uuid populationId, EffectiveAccessView row) =>
        Uuid.CreateVersion5(campaignId,
            $"{populationId}\n{row.ProviderSubjectId}\n{row.ProviderEntitlementId}");

    static Result<AccessReviewCampaignRegistration> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessReviewCampaignRegistration>(kind, message);
}
