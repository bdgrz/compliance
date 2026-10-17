using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the revise significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.revise-significant-change", 1)]
public sealed record ReviseSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid ChangeId, long ExpectedRevision, string Title, string Description, DateOnly EffectiveOn, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
