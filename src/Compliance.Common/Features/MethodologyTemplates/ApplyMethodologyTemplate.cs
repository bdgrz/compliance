using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the apply methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.apply-methodology-template", 1)]
public sealed record ApplyMethodologyTemplate(Uuid TenantId, Uuid ProgramId, Uuid TemplateId, long TemplateVersion, string ApplicationKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
