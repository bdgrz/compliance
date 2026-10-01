using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Freezes the final campaign snapshot (population, decisions with history, provider changes,
///     verifications, and exceptions) and records the owner's completion sign-off. HTTP-only.
/// </summary>
public sealed class CompleteAccessReviewCampaignHandler(IAggregateReader reader,
    IAggregateExecutor executor, PopulationSnapshotFreezer freezer, TimeProvider clock)
    : IRequestHandler<CompleteAccessReviewCampaign, AccessReviewCampaignCompletionView>
{
    public async ValueTask<Result<AccessReviewCampaignCompletionView>> HandleAsync(
        IRequestContext<CompleteAccessReviewCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(request.TenantId,
            request.CampaignId), ct).ConfigureAwait(false);
        if (campaign.Launched is not { } launched)
            return Failure(RequestErrorKind.NotFound, "The campaign was not found.");
        if (campaign.Completion is { } completed)
            return completed.SnapshotId == context.RequestId
                ? Result<AccessReviewCampaignCompletionView>.Success(completed)
                : Failure(RequestErrorKind.Conflict, "The campaign was already completed.");
        if (campaign.Revision != request.ExpectedRevision)
            return Failure(RequestErrorKind.Conflict,
                $"The campaign is at revision {campaign.Revision}; expected {request.ExpectedRevision}.");
        var now = clock.GetUtcNow();
        if (campaign.CompletionBlockers(now) is { Count: > 0 } blockers)
            return Failure(RequestErrorKind.Conflict, string.Join(" ", blockers.Take(10)));
        if (!AccessReviewVocabulary.IsBoundedText(request.Attestation, 4000))
            return Failure(RequestErrorKind.Validation,
                "Completion requires an attestation of at most 4000 characters.");

        var actor = AccessReviewActor.From(context);
        var header = new AccessReviewCampaignResultHeader(campaign.Id, launched.Name,
            launched.Deadline, launched.SnapshotId, launched.ContentSha256, request.Attestation.Trim());
        var frozen = await freezer.FreezeAsync(context, request.TenantId,
            AccessReviewCampaignContent.ResultKind,
            AccessReviewCampaignContent.ResultRows(header, campaign.ItemStates(now)), null, null,
            actor.Reference, ct).ConfigureAwait(false);
        if (!frozen.IsSuccess)
            return Result<AccessReviewCampaignCompletionView>.Failure(frozen.Error);
        return await executor.ExecuteAsync(new AccessReviewCampaign(request.TenantId,
                request.CampaignId),
            current => AccessReviewOutcome.From(current.Complete(request.ExpectedRevision,
                frozen.Value.SnapshotId, frozen.Value.ContentSha256, request.Attestation,
                actor.Reference, now)), context, ct).ConfigureAwait(false);
    }

    static Result<AccessReviewCampaignCompletionView> Failure(RequestErrorKind kind,
        string message) => AccessReviewOutcome.Failure<AccessReviewCampaignCompletionView>(kind, message);
}
