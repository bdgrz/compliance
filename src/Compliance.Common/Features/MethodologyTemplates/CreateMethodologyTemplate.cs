using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the create methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.create-methodology-template", 1)]
public sealed record CreateMethodologyTemplate(Uuid TemplateId, string Name, string Description, string TemplateKind) : IRequest, IPlatformOperatorRequest, ICallable;
