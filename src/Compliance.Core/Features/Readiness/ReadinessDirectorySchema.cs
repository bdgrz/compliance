using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

static class ReadinessDirectorySchema
{
    public static readonly KvDirectoryIndex<ReadinessAssessmentSummaryProjection> AssessmentsByProgram =
        new("assessments_by_program", 1, static view =>
            [view.ProgramId.ToString(), DescendingRevision(view.Revision)]);

    public static readonly KvDirectory<ReadinessAssessmentSummaryProjection, Uuid> Assessments =
        new("readiness_assessments", ComplianceCoreJsonContext.Default.ReadinessAssessmentSummaryProjection,
            static view => view.AssessmentId, static id => [id.ToString()], [AssessmentsByProgram]);

    public static readonly KvDirectory<ReadinessAssessmentProjection, Uuid> AssessmentDetails =
        new("readiness_assessment_details", ComplianceCoreJsonContext.Default.ReadinessAssessmentProjection,
            static view => view.AssessmentId, static id => [id.ToString()], []);

    public static readonly KvDirectory<ReadinessProgramProjection, Uuid> Programs =
        new("readiness_programs", ComplianceCoreJsonContext.Default.ReadinessProgramProjection,
            static view => view.ProgramId, static id => [id.ToString()], []);

    public static readonly KvDirectory<ReadinessGapPlanProjection, Uuid> GapPlans =
        new("readiness_gap_plans", ComplianceCoreJsonContext.Default.ReadinessGapPlanProjection,
            static view => view.Plan.GapId, static id => [id.ToString()], []);

    public static readonly KvDirectoryIndex<ReadinessAnnotationProjection> AnnotationsByAssessment =
        new("annotations_by_assessment", 1, static view =>
            [view.ProgramId.ToString(), view.Annotation.AssessmentId.ToString(),
                view.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ReadinessAnnotationProjection, Uuid> Annotations =
        new("readiness_annotations", ComplianceCoreJsonContext.Default.ReadinessAnnotationProjection,
            static view => view.Annotation.AnnotationId, static id => [id.ToString()],
            [AnnotationsByAssessment]);

    public static readonly KvDirectoryIndex<ReadinessTypeIEntryDecisionProjection> TypeIEntriesByProgram =
        new("type_i_entries_by_program", 1, static view =>
            [view.ProgramId.ToString(), DescendingRevision(view.Revision)]);

    public static readonly KvDirectory<ReadinessTypeIEntryDecisionProjection, Uuid> TypeIEntries =
        new("readiness_type_i_entries", ComplianceCoreJsonContext.Default.ReadinessTypeIEntryDecisionProjection,
            static view => view.Decision.DecisionId, static id => [id.ToString()],
            [TypeIEntriesByProgram]);

    static string DescendingRevision(long revision) =>
        (long.MaxValue - revision).ToString("D20", CultureInfo.InvariantCulture);
}

public sealed record ReadinessAssessmentSummaryProjection(Uuid TenantId, Uuid ProgramId,
    Uuid AssessmentId, long Revision, ReadinessAssessmentSummaryView Summary);

public sealed record ReadinessAssessmentProjection(Uuid TenantId, Uuid ProgramId,
    Uuid AssessmentId, ReadinessAssessmentRecorded Recorded,
    ReadinessDecisionView? Decision, TypeIEntryDecisionView? TypeIEntryDecision);

public sealed record ReadinessProgramProjection(Uuid TenantId, Uuid ProgramId,
    long Revision, Uuid LatestAssessmentId);

public sealed record ReadinessGapPlanProjection(Uuid TenantId, Uuid ProgramId,
    long Revision, ReadinessGapPlanView Plan);

public sealed record ReadinessAnnotationProjection(Uuid TenantId, Uuid ProgramId,
    long Revision, ReadinessAnnotationView Annotation);

public sealed record ReadinessTypeIEntryDecisionProjection(Uuid TenantId, Uuid ProgramId,
    long Revision, TypeIEntryDecisionView Decision);
