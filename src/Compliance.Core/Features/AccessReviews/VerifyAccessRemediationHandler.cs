using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Verifies remediation from a later accepted population of the same system instance: a
///     revoked item must be absent, and a modified item must be absent or reach the entitlement
///     differently. A ticket or provider change alone is never verification (M0-D07).
/// </summary>
public sealed class VerifyAccessRemediationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<VerifyAccessRemediation, AccessRemediationVerificationView>
{
    public async ValueTask<Result<AccessRemediationVerificationView>> HandleAsync(
        IRequestContext<VerifyAccessRemediation> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        if (campaign.FindItem(request.ItemId) is not { } item)
            return Failure(RequestErrorKind.NotFound, "The review item was not found.");
        if (campaign.CurrentDecision(request.ItemId) is not { } decision ||
            !AccessReviewVocabulary.RequiresRemediation(decision.Decision))
            return Failure(RequestErrorKind.Conflict,
                "Only an item decided modify or revoke requires remediation.");
        var loaded = await AcceptedAccessPopulation.LoadAsync(reader, request.TenantId,
            request.PopulationId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<AccessRemediationVerificationView>.Failure(loaded.Error);
        var later = loaded.Value;
        if (later.Header.SystemInstanceId != item.SystemInstanceId || later.Population.Id == item.PopulationId)
            return Failure(RequestErrorKind.Validation,
                "Verification requires a later accepted population of the same system instance.");
        if (later.Header.ObservedAt <= decision.DecidedAt)
            return Failure(RequestErrorKind.Conflict,
                "Verification requires a population observed after the decision.");
        var row = later.EffectiveAccess.FirstOrDefault(access =>
            access.ProviderSubjectId == item.ProviderSubjectId &&
            access.ProviderEntitlementId == item.ProviderEntitlementId);
        string outcome;
        if (row is null)
            outcome = "removed";
        else if (decision.Decision == AccessReviewVocabulary.Modify && !SameAccess(row, item))
            outcome = "changed";
        else
            return Failure(RequestErrorKind.Conflict,
                "The later population still shows the access unchanged.");
        var actor = AccessReviewActor.From(context);
        var verification = new AccessRemediationVerificationView(later.Population.Id,
            later.Snapshot.Id, later.CalculationId, later.Header.ObservedAt, outcome,
            actor.Reference, clock.GetUtcNow());
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            current => AccessReviewOutcome.From(current.Verify(request.ItemId,
                request.ExpectedRevision, verification)), context, ct).ConfigureAwait(false);
    }

    static bool SameAccess(EffectiveAccessView row, AccessReviewItemView item) =>
        row.ExpiresAt == item.ExpiresAt && row.Paths.Count == item.Paths.Count &&
        row.Paths.Zip(item.Paths).All(static pair => pair.First.ExpiresAt == pair.Second.ExpiresAt &&
                                                     pair.First.Hops.SequenceEqual(pair.Second.Hops));

    static Result<AccessRemediationVerificationView> Failure(RequestErrorKind kind, string message) =>
        AccessReviewOutcome.Failure<AccessRemediationVerificationView>(kind, message);
}
