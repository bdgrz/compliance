using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Workforce;
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
    TimeProvider clock)
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
            return Result<AccessReviewCampaignRegistration>.Success(new(campaignId,
                launched.SnapshotId, launched.ContentSha256, launched.Items.Count));
        var now = clock.GetUtcNow();
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
            var loaded = await AcceptedAccessPopulation.LoadAsync(reader, request.TenantId,
                assignment.PopulationId, ct).ConfigureAwait(false);
            if (!loaded.IsSuccess)
                return Result<AccessReviewCampaignRegistration>.Failure(loaded.Error);
            var accepted = loaded.Value;
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

        var actor = AccessReviewActor.From(context);
        var header = new AccessReviewCampaignHeader(campaignId, request.Name.Trim(),
            request.Instructions.Trim(), request.Deadline);
        var frozen = await freezer.FreezeAsync(context, request.TenantId,
            AccessReviewCampaignContent.LaunchKind,
            AccessReviewCampaignContent.LaunchRows(header, reviewers, items), null, null,
            actor.Reference, ct).ConfigureAwait(false);
        if (!frozen.IsSuccess)
            return Result<AccessReviewCampaignRegistration>.Failure(frozen.Error);
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId, campaignId),
            campaign => AccessReviewOutcome.From(campaign.Launch(request.Name, request.Instructions,
                request.Deadline, frozen.Value.SnapshotId, frozen.Value.ContentSha256, reviewers,
                items, actor.Reference, now)), context, ct).ConfigureAwait(false);
    }

    static Uuid ItemId(Uuid campaignId, Uuid populationId, EffectiveAccessView row) =>
        Uuid.CreateVersion5(campaignId,
            $"{populationId}\n{row.ProviderSubjectId}\n{row.ProviderEntitlementId}");

    static Result<AccessReviewCampaignRegistration> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessReviewCampaignRegistration>(kind, message);
}
