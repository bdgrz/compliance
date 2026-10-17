using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the record audit package delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.record-audit-package-delivery", 1)]
public sealed record RecordAuditPackageDelivery(Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, string Recipient, DateTimeOffset DeliveredAt, string DeliveryReference) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
