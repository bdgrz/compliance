using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-populations.list-audit-population-rows", 1)]
public sealed record ListAuditPopulationRows(Uuid TenantId, Uuid ProgramId, Uuid AuditPopulationId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuditPopulationRowView>>, IProgramReadRequest, ICallable;
