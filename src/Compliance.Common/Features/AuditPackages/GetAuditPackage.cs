using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-packages.get-audit-package", 1)]
public sealed record GetAuditPackage(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageId)
    : IRequest<AuditPackageView>, IProgramReadRequest, ICallable;
