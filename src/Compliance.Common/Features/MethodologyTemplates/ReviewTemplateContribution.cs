using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Records the review template contribution intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.review-template-contribution", 1)]
public sealed record ReviewTemplateContribution(Uuid ContributionId, string Outcome, string ReviewNotes) : IRequest, IPlatformOperatorRequest, ICallable;
