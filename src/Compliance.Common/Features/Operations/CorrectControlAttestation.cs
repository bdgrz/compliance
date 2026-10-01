using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Corrects a submitted attestation by a new version with a reason; HTTP-only.</summary>
[Discriminator("bdgrz.control.occurrence.correct", 1)]
public sealed record CorrectControlAttestation(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, string Result, DateTimeOffset PerformedAt,
    DateOnly? CoveredFrom, DateOnly? CoveredUntil, string? Notes, string? Rationale,
    IReadOnlyList<EvidenceReference> Evidence, string CorrectionReason,
    Uuid? PerformedByPersonId = null)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, ICallable;
