using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.methodology-templates.get-methodology-template-version", 1)]
public sealed record GetMethodologyTemplateVersion(Uuid TenantId, Uuid ProgramId, Uuid MethodologyTemplateVersionId)
    : IRequest<MethodologyTemplateVersionView>, IProgramReadRequest, ICallable;
