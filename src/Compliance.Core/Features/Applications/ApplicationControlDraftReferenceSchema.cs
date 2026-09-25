using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationControlDraftReferenceSchema
{
    public static readonly KvDirectoryIndex<ApplicationControlDraftReferenceView> ByRecord = new(
        "by_governed_record", 1, static reference =>
        [reference.SubjectType, reference.GovernedRecordId.ToString(),
            reference.ControlId.ToString(), reference.EntryId.ToString()]);

    public static readonly KvDirectory<ApplicationControlDraftReferenceView, (Uuid ControlId,
        Uuid EntryId)> References = new("application_control_draft_references",
        ComplianceCoreJsonContext.Default.ApplicationControlDraftReferenceView,
        static reference => (reference.ControlId, reference.EntryId),
        static key => [key.ControlId.ToString(), key.EntryId.ToString()], [ByRecord]);

    public static readonly KvDirectory<ControlDraftReferenceState, Uuid> States = new(
        "control_draft_reference_states",
        ComplianceCoreJsonContext.Default.ControlDraftReferenceState,
        static state => state.ControlId, static id => [id.ToString()], []);
}
