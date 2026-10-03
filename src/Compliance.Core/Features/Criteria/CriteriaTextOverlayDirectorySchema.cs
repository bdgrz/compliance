using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

static class CriteriaTextOverlayDirectorySchema
{
    public static readonly KvDirectory<CriteriaTextOverlayRevision, Uuid> Overlays = new(
        "criteria_text_overlays",
        ComplianceCoreJsonContext.Default.CriteriaTextOverlayRevision,
        static revision => Key(revision.EditionId, revision.Identifier),
        static id => [id.ToString()], []);

    public static Uuid Key(Uuid editionId, string identifier) =>
        Uuid.CreateVersion5(editionId, "criteria-text-overlay:" + identifier);
}
