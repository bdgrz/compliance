using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.FirmPortfolio;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.firm-portfolio.get-authorized-client-summary", 1)]
public sealed record GetAuthorizedClientSummary(Uuid TenantId, Uuid ClientTenantId)
    : IRequest<AuthorizedClientSummaryView>, ITenantAccessRequest, ICallable;
