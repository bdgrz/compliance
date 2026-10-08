using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Appends authored report facts at the expected revision; the provider cannot change.</summary>
[Discriminator("bdgrz.provider.assurance_report.revise", 1)]
public sealed record ReviseAssuranceReport(Uuid TenantId, Uuid ProviderId, Uuid ReportId,
    long ExpectedRevision, AssuranceReportContent Content)
    : IRequest<AssuranceReportRegistration>, IProviderManagementRequest, IClientManagementMutationRequest, ICallable;
