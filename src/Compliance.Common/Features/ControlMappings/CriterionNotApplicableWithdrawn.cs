using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.criterion_applicability.not_applicable_withdrawn", 1)]
public sealed record CriterionNotApplicableWithdrawn(Uuid TenantId, Uuid ProgramId,
    Uuid DecisionId, long Revision, int VersionNumber, string Rationale, ActorReference Actor,
    DateTimeOffset WithdrawnAt) : DomainEvent;
