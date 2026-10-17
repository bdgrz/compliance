using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.FirmPortfolio;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.firm-portfolio.list-authorized-client-portfolio", 1)]
public sealed record ListAuthorizedClientPortfolio(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuthorizedClientSummaryView>>, ITenantAccessRequest, ICallable;
