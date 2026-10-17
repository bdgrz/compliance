using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the deliver audit response intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.deliver-audit-response", 1)]
public sealed record DeliverAuditResponse(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId, long Version, string Recipient, string DeliveryReference) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
