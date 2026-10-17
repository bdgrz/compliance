using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the record auditor response disposition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.record-auditor-response-disposition", 1)]
public sealed record RecordAuditorResponseDisposition(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId, long Version, string Disposition, string? ExternalReference) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
