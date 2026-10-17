using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the propose template successor adoption intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.propose-template-successor-adoption", 1)]
public sealed record ProposeTemplateSuccessorAdoption(Uuid TenantId, Uuid ProgramId, Uuid TemplateId, long NewTemplateVersion, string Disposition) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
