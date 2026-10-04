using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

static class RestrictedAccessReviewVisibility
{
    public static async ValueTask<bool> CanReadPopulationAsync(IAggregateReader reader,
        RestrictedApplicationVisibility visibility, Uuid tenantId, Uuid userId,
        Uuid populationId, CancellationToken ct)
    {
        var population = await reader.HydrateAsync(new AccessPopulation(tenantId, populationId), ct)
            .ConfigureAwait(false);
        return population.Opened is { } opened &&
               await visibility.CanReadSystemInstanceAsync(tenantId, userId,
                   opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false);
    }

    public static async ValueTask<bool> CanReadCampaignItemsAsync(
        RestrictedApplicationVisibility visibility, Uuid tenantId, Uuid userId,
        AccessReviewCampaign campaign, IReadOnlyCollection<Uuid>? itemIds = null,
        CancellationToken ct = default)
    {
        if (campaign.Launched is not { } launched)
            return false;

        var items = launched.Items.AsEnumerable();
        if (itemIds is not null)
        {
            var selected = new List<AccessReviewItemView>(itemIds.Count);
            foreach (var itemId in itemIds.Distinct())
            {
                var item = launched.Items.FirstOrDefault(candidate => candidate.ItemId == itemId);
                if (item is null)
                    return false;
                selected.Add(item);
            }
            items = selected;
        }

        foreach (var systemInstanceId in items.Select(static item => item.SystemInstanceId)
                     .Distinct())
        {
            if (!await visibility.CanReadSystemInstanceAsync(tenantId, userId,
                    systemInstanceId, ct).ConfigureAwait(false))
                return false;
        }
        return true;
    }
}
