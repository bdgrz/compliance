using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     A program's reviewed decision that one criterion of an edition does not apply, keyed by
///     (edition_id, criterion_identifier). Status is pending, not_applicable, rejected, or
///     withdrawn. A not-applicable decision excludes the criterion from coverage; it never
///     states that the criterion is satisfied.
/// </summary>
public sealed record CriterionApplicabilityView(Uuid TenantId, Uuid ProgramId, Uuid DecisionId,
    Uuid EditionId, string CriterionIdentifier, long Revision, string Status,
    int? ActiveVersionNumber, IReadOnlyList<CriterionApplicabilityVersionView> Versions);
