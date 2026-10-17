using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.incidents.list-compliance-incidents", 1)]
public sealed record ListComplianceIncidents(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ComplianceIncidentSummaryView>>, IProgramReadRequest, ICallable;
