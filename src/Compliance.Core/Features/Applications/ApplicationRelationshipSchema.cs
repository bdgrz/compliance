using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationRelationshipSchema
{
    public static readonly KvDirectoryIndex<ApplicationRelationshipView> BySourceApplication = new(
        "by_source_application", 1, static relationship =>
        [relationship.SourceApplicationId.ToString(), relationship.RelationshipId.ToString()]);

    public static readonly KvDirectoryIndex<ApplicationRelationshipView> ByTargetApplication = new(
        "by_target_application", 1, static relationship =>
        [relationship.TargetApplicationId.ToString(), relationship.RelationshipId.ToString()]);

    public static readonly KvDirectory<ApplicationRelationshipView, Uuid> Relationships = new(
        "application_relationships",
        ComplianceCoreJsonContext.Default.ApplicationRelationshipView,
        static relationship => relationship.RelationshipId,
        static id => [id.ToString()], [BySourceApplication, ByTargetApplication]);
}
