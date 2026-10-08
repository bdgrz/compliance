using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Records authored report facts; this asserts no coverage or approval.</summary>
[Discriminator("bdgrz.provider.assurance_report.record", 1)]
public sealed record RecordAssuranceReport(Uuid TenantId, Uuid ProviderId, AssuranceReportContent Content)
    : IRequest<AssuranceReportRegistration>, IProviderManagementRequest, IClientManagementMutationRequest, ICallable;
