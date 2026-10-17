using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.methodology-templates.preview-methodology-template-application", 1)]
public sealed record PreviewMethodologyTemplateApplication(Uuid TenantId, Uuid ProgramId, Uuid TemplateId)
    : IRequest<MethodologyTemplateApplicationPreview>, IProgramReadRequest, ICallable;
