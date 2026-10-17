using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the record auditor request intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.record-auditor-request", 1)]
public sealed record RecordAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ExaminationId, string Source, string ExternalReference, string Wording, string Scope, string Priority, Uuid OwnerMemberId, DateOnly DueOn) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
