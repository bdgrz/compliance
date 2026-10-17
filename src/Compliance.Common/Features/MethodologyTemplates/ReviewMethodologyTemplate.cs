using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the review methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.review-methodology-template", 1)]
public sealed record ReviewMethodologyTemplate(Uuid TemplateId, long Version, string Outcome, string Comments) : IRequest, IPlatformOperatorRequest, ICallable;
