using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.methodology-templates.get-template-application", 1)]
public sealed record GetTemplateApplication(Uuid TenantId, Uuid ProgramId, Uuid TemplateApplicationId)
    : IRequest<TemplateApplicationView>, IProgramReadRequest, ICallable;
