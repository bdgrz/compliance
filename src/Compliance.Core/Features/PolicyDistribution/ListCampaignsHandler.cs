using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class ListCampaignsHandler(ICampaignDirectoryReader directory,
    IDomainEventReader events)
    : IRequestHandler<ListCampaigns, Page<CampaignSummaryView>>
{
    public async ValueTask<Result<Page<CampaignSummaryView>>> HandleAsync(
        IRequestContext<ListCampaigns> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<CampaignSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        await using (var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                         request.TenantId.ToString(), "policy-distribution-campaigns"),
                     checkpoint.Cursor, ct).GetAsyncEnumerator(ct))
        {
            if (await pending.MoveNextAsync().ConfigureAwait(false))
                return Result<Page<CampaignSummaryView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The campaign list projection has not reached the source.", isTransient: true));
        }
        Page<CampaignSummaryView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<CampaignSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The campaign cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<CampaignSummaryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The campaign projection has an invalid program scope."))
            : Result<Page<CampaignSummaryView>>.Success(page);
    }
}
