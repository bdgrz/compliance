using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.FirmPortfolio;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.firm-portfolio.list-my-cross-client-work", 1)]
public sealed record ListMyCrossClientWork(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CrossClientWorkItemView>>, ITenantAccessRequest, ICallable;
