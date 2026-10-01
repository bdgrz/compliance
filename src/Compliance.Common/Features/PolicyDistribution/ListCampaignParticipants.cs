using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Lists individually inspectable people and results. Person-level workforce and training
///     detail requires program management, not program read access.
/// </summary>
[Discriminator("bdgrz.campaign.participants.list", 1)]
public sealed record ListCampaignParticipants(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    DateOnly? AsOf = null, string? State = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CampaignParticipantView>>, IProgramScopedRequest, ICallable;
