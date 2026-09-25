using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationBoundaryReferenceSchema
{
    public static readonly KvDirectoryIndex<ApplicationBoundaryReferenceView> ByRecord = new(
        "by_governed_record", 1, static reference =>
        [reference.SubjectType, reference.GovernedRecordId.ToString(),
            reference.BoundaryId.ToString(), reference.VersionId.ToString(),
            reference.EntryId.ToString()]);

    public static readonly KvDirectory<ApplicationBoundaryReferenceView,
        (Uuid BoundaryId, Uuid VersionId, Uuid EntryId)> References = new(
        "application_boundary_references",
        ComplianceCoreJsonContext.Default.ApplicationBoundaryReferenceView,
        static reference => (reference.BoundaryId, reference.VersionId, reference.EntryId),
        static key => [key.BoundaryId.ToString(), key.VersionId.ToString(),
            key.EntryId.ToString()], [ByRecord]);

    public static readonly KvDirectory<BoundaryReferenceState, Uuid> States = new(
        "boundary_reference_states", ComplianceCoreJsonContext.Default.BoundaryReferenceState,
        static state => state.BoundaryId, static id => [id.ToString()], []);
}
