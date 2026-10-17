using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records the bind system description to examination intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.system-description.bind-system-description-to-examination", 1)]
public sealed record BindSystemDescriptionToExamination(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, Uuid ExaminationId, string BindingPurpose) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
