using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the approve system description intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.approve-system-description", 1)]
public sealed record ApproveSystemDescription(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
