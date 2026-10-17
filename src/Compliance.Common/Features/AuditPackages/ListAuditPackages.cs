using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-packages.list-audit-packages", 1)]
public sealed record ListAuditPackages(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuditPackageSummaryView>>, IProgramReadRequest, ICallable;
