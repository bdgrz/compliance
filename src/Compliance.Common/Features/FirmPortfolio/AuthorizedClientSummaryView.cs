using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.FirmPortfolio;

/// <summary>Client summary visible within the actor's authorized portfolio.</summary>
public sealed record AuthorizedClientSummaryView(Uuid TenantId, Uuid ClientTenantId,
    string Name, string Status, int ProgramCount, int OpenWorkCount, DateTimeOffset AsOf);
