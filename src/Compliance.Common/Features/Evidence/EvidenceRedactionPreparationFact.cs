using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed record EvidenceRedactionPreparationFact(Uuid PreparationId, long Revision,
    EvidenceRedactionSourceCapsule Original, EvidenceRedactionSourceCapsule Derived,
    string Provenance, string Reason, ActorReference PreparedBy, DateTimeOffset PreparedAt);
