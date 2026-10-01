using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.occurrence.attested", 1)]
public sealed record ControlOccurrenceAttested(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long Revision, ControlAttestationView Attestation) : DomainEvent;
