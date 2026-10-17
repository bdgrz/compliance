using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the revise system description draft intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.revise-system-description-draft", 1)]
public sealed record ReviseSystemDescriptionDraft(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long ExpectedRevision, string Sections, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
