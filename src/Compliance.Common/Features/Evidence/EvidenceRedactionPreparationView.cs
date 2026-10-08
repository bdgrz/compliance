using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

public sealed record EvidenceRedactionPreparationView(Uuid PreparationId, long Revision,
    EvidenceRedactionArtifactView Original, EvidenceRedactionArtifactView Derived,
    string Provenance, string Reason, ActorReference PreparedBy, DateTimeOffset PreparedAt);
