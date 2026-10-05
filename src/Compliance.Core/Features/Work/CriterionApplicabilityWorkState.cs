using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record CriterionApplicabilityWorkState(Uuid TenantId, Uuid ProgramId,
    Uuid DecisionId, Uuid EditionId, string CriterionIdentifier, long Revision,
    int VersionNumber, Uuid ProposerMemberId, DateTimeOffset ProposedAt, string State);
