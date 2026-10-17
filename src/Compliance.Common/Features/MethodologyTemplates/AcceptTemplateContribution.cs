using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the accept template contribution intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.accept-template-contribution", 1)]
public sealed record AcceptTemplateContribution(Uuid ContributionId, string AcceptanceRationale) : IRequest, IPlatformOperatorRequest, ICallable;
