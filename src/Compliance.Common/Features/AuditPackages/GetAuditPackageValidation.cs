using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-packages.get-audit-package-validation", 1)]
public sealed record GetAuditPackageValidation(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageValidationId)
    : IRequest<AuditPackageValidationView>, IProgramReadRequest, ICallable;
