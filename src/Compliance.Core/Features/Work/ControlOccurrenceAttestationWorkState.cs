using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlOccurrenceAttestationWorkState(Uuid AttestationId, int Version,
    Uuid RecorderMemberId, OperatingHolder PerformedBy, DateTimeOffset RecordedAt);
