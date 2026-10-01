using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.criterion_applicability.not_applicable_proposed", 1)]
public sealed record CriterionNotApplicableProposed(Uuid TenantId, Uuid ProgramId,
    Uuid DecisionId, long Revision, int VersionNumber, Uuid EditionId,
    string CriterionIdentifier, string Rationale, Uuid ProposerMemberId, ActorReference Actor,
    DateTimeOffset ProposedAt) : DomainEvent;
