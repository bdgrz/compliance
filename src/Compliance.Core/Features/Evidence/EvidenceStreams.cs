using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

static class EvidenceStreams
{
    public const string Area = "evidence-artifacts";

    public static EventStreamAddress Address(Uuid tenantId, Uuid artifactId) =>
        new(tenantId.ToString(), Area, artifactId.ToString());
}
