using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     A mapping between a control and an immutable catalog entry, keyed by
///     (edition_id, criterion_identifier). Status is pending, active, rejected, or retired.
///     A mapping records a claimed relationship; it never asserts that a criterion is satisfied.
/// </summary>
public sealed record ControlCriterionMappingView(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, Uuid ControlId, Uuid EditionId, string CriterionIdentifier,
    string CriterionKind, long Revision, string Status, int? ActiveVersionNumber,
    Uuid? ActiveControlVersionId, IReadOnlyList<ControlCriterionMappingVersionView> Versions);
