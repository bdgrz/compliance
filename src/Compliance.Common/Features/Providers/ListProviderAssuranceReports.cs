using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.assurance_reports.list", 1)]
public sealed record ListProviderAssuranceReports(Uuid TenantId, Uuid ProviderId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AssuranceReportView>>, IProviderRequest, ICallable;
