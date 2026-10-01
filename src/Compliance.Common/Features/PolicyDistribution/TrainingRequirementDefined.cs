using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Defines a requirement's first version or an immutable successor version.</summary>
[Discriminator("bdgrz.training_requirement.defined", 1)]
public sealed record TrainingRequirementDefined(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, string Identifier, long Version, TrainingRequirementContent Content,
    string ContentSha256, Uuid DefineRequestId, ActorReference Actor, DateTimeOffset ChangedAt)
    : DomainEvent;
