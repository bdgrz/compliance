using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the amend audit package intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.amend-audit-package", 1)]
public sealed record AmendAuditPackage(Uuid TenantId, Uuid ProgramId, Uuid PackageId, long ExpectedRevision, string AmendmentReason, IReadOnlyList<Uuid> IncludedRecordIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
