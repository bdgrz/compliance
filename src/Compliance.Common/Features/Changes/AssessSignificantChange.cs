using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the assess significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.assess-significant-change", 1)]
public sealed record AssessSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid ChangeId, long Version, IReadOnlyList<Uuid> AffectedRecordIds, string Significance, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
