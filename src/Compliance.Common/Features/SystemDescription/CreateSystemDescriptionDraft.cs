using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the create system description draft intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.create-system-description-draft", 1)]
public sealed record CreateSystemDescriptionDraft(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ExaminationId, string Title) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
