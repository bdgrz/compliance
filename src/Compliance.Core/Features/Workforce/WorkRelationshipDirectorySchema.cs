using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class WorkRelationshipDirectorySchema
{
    public static readonly KvDirectoryIndex<WorkRelationshipView> BySourceWorkerId = new(
        "by_source_worker_id", 1,
        static view => [view.SourceWorkerId, view.RelationshipId.ToString()]);

    public static readonly KvDirectory<WorkRelationshipView, Uuid> Relationships = new(
        "work-relationships", ComplianceCoreJsonContext.Default.WorkRelationshipView,
        static view => view.RelationshipId,
        static relationshipId => [relationshipId.ToString()], [BySourceWorkerId]);
}
