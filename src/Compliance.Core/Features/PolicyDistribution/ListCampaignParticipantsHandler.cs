using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed class ListCampaignParticipantsHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ListCampaignParticipants, Page<CampaignParticipantView>>
{
    static readonly string[] States =
        ["pending", "overdue", "acknowledged", "completed", "excepted", "removed"];

    public async ValueTask<Result<Page<CampaignParticipantView>>> HandleAsync(
        IRequestContext<ListCampaignParticipants> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.State is { } state && !States.Contains(state))
            return Result<Page<CampaignParticipantView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The state must be pending, overdue, acknowledged, completed, excepted, or removed."));
        var campaign = await CampaignSource.ReadAsync(reader, request.TenantId,
            request.ProgramId, request.CampaignId, ct).ConfigureAwait(false);
        return campaign.IsSuccess
            ? ControlActivationSource.Paginate(campaign.Value.Participants(request.AsOf ??
                PolicySource.Today(clock), request.State), request.Limit, request.Cursor,
                "campaign participants")
            : Result<Page<CampaignParticipantView>>.Failure(campaign.Error);
    }
}
