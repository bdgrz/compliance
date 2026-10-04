using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Lists from the Fitz campaign directory once it has reached the source.</summary>
public sealed class ListAccessReviewCampaignsHandler(IAccessReviewCampaignDirectoryReader directory,
    IDomainEventReader events, IAggregateReader reader,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListAccessReviewCampaigns, Page<AccessReviewCampaignSummaryView>>
{
    public async ValueTask<Result<Page<AccessReviewCampaignSummaryView>>> HandleAsync(
        IRequestContext<ListAccessReviewCampaigns> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Validation, "The campaign list limit must be between 1 and 200.");
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!await AccessReviewOutcome.IsCaughtUpAsync(events,
                AccessReviewDirectorySchema.CampaignPattern(request.TenantId), checkpoint, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Conflict, "The campaign directory has not reached the source.", true);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        Page<AccessReviewCampaignSummaryView> page;
        var visibleItemCounts = new Dictionary<Uuid, int>();
        try
        {
            page = await VisibleApplicationPage.ReadAsync(request.Limit ?? 50, request.Cursor,
                (limit, cursor) => directory.ListAsync(request.TenantId, limit, cursor, ct),
                async item =>
                {
                    var campaign = await reader.HydrateAsync(new AccessReviewCampaign(
                        request.TenantId, item.CampaignId), ct).ConfigureAwait(false);
                    if (campaign.Launched is not { } launched)
                        return false;
                    var visibleInstances = new HashSet<Uuid>();
                    foreach (var systemInstanceId in launched.Items.Select(
                                 static reviewItem => reviewItem.SystemInstanceId).Distinct())
                    {
                        if (await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                                systemInstanceId, ct).ConfigureAwait(false))
                            visibleInstances.Add(systemInstanceId);
                    }
                    var count = launched.Items.Count(reviewItem =>
                        visibleInstances.Contains(reviewItem.SystemInstanceId));
                    visibleItemCounts[item.CampaignId] = count;
                    return count > 0;
                }, item => item.TenantId == request.TenantId).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Validation, "The campaign cursor is invalid.");
        }
        catch (VisibleApplicationPage.ForeignDirectoryItemException)
        {
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.NotFound, "The campaigns were not found.");
        }
        return Result<Page<AccessReviewCampaignSummaryView>>.Success(page with
        {
            Items = page.Items.Select(item => item with
            {
                ItemCount = visibleItemCounts[item.CampaignId],
            }).ToArray(),
        });
    }
}
