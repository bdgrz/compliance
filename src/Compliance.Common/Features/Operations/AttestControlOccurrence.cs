using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The performer's personal attestation of one occurrence; HTTP-only. A member may record a
///     workforce person's off-product performance; both are attributed.
/// </summary>
[Discriminator("bdgrz.control.occurrence.attest", 1)]
public sealed record AttestControlOccurrence(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, string Result, DateTimeOffset PerformedAt,
    DateOnly? CoveredFrom, DateOnly? CoveredUntil, string? Notes, string? Rationale,
    IReadOnlyList<EvidenceReference> Evidence, Uuid? PerformedByPersonId = null)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, ICallable;
