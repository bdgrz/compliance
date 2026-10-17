using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the propose system description successor intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.propose-system-description-successor", 1)]
public sealed record ProposeSystemDescriptionSuccessor(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long ExpectedRevision, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
