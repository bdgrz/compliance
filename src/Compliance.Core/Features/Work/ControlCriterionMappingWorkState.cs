using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlCriterionMappingWorkState(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, Uuid ControlId, Uuid ControlVersionId, Uuid EditionId,
    string CriterionIdentifier, string CriterionKind, long Revision, int LastVersionNumber,
    int? ActiveVersionNumber, int? PendingVersionNumber, Uuid? ProposerMemberId,
    DateTimeOffset? ProposedAt, string State);
