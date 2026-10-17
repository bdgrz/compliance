using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the retire methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.retire-methodology-template", 1)]
public sealed record RetireMethodologyTemplate(Uuid TemplateId, long ExpectedRevision, DateOnly EffectiveOn, string Rationale) : IRequest, IPlatformOperatorRequest, ICallable;
