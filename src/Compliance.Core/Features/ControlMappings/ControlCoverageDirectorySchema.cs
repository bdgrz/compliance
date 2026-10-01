using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

static class ControlCoverageDirectorySchema
{
    public static readonly KvDirectoryIndex<ControlCriterionMappingView> MappingsByProgram = new(
        "by_program_criterion", 1, static mapping =>
        [
            mapping.ProgramId.ToString(), mapping.EditionId.ToString(),
            mapping.CriterionIdentifier, mapping.MappingId.ToString(),
        ]);

    public static readonly KvDirectory<ControlCriterionMappingView, Uuid> Mappings = new(
        "control_mappings_v1", ComplianceCoreJsonContext.Default.ControlCriterionMappingView,
        static mapping => mapping.MappingId,
        static mappingId => [mappingId.ToString()], [MappingsByProgram]);

    public static readonly KvDirectoryIndex<CriterionApplicabilityView> DecisionsByProgram = new(
        "by_program_criterion", 1, static decision =>
        [
            decision.ProgramId.ToString(), decision.EditionId.ToString(),
            decision.CriterionIdentifier,
        ]);

    public static readonly KvDirectory<CriterionApplicabilityView, Uuid> Decisions = new(
        "criterion_applicability_v1",
        ComplianceCoreJsonContext.Default.CriterionApplicabilityView,
        static decision => decision.DecisionId,
        static decisionId => [decisionId.ToString()], [DecisionsByProgram]);
}
