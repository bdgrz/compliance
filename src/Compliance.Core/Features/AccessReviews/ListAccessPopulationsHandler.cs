using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Lists from the Fitz population directory once it has reached the source.</summary>
public sealed class ListAccessPopulationsHandler(IAccessPopulationDirectoryReader directory,
    IDomainEventReader events)
    : IRequestHandler<ListAccessPopulations, Page<AccessPopulationSummaryView>>
{
    public async ValueTask<Result<Page<AccessPopulationSummaryView>>> HandleAsync(
        IRequestContext<ListAccessPopulations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return AccessReviewOutcome.Failure<Page<AccessPopulationSummaryView>>(
                RequestErrorKind.Validation, "The population list limit must be between 1 and 200.");
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!await AccessReviewOutcome.IsCaughtUpAsync(events,
                AccessReviewDirectorySchema.PopulationPattern(request.TenantId), checkpoint, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<Page<AccessPopulationSummaryView>>(
                RequestErrorKind.Conflict, "The population directory has not reached the source.", true);
        Page<AccessPopulationSummaryView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.SystemInstanceId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return AccessReviewOutcome.Failure<Page<AccessPopulationSummaryView>>(
                RequestErrorKind.Validation, "The population cursor is invalid.");
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.SystemInstanceId != request.SystemInstanceId)
            ? AccessReviewOutcome.Failure<Page<AccessPopulationSummaryView>>(
                RequestErrorKind.NotFound, "The populations were not found.")
            : Result<Page<AccessPopulationSummaryView>>.Success(page);
    }
}
