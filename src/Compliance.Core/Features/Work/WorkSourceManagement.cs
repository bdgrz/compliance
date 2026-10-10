using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Resolves the source commands behind queue actions; their mutation metadata controls the wall.</summary>
static class WorkSourceManagement
{
    internal static readonly IReadOnlyDictionary<string, Type[]> SourceRequests = new Dictionary<string, Type[]>
    {
        [WorkSource.ControlOccurrence] = [typeof(OpenControlOccurrence), typeof(AttestControlOccurrence), typeof(CorrectControlAttestation)],
        [WorkSource.OccurrenceReview] = [typeof(ReviewControlOccurrence)],
        [WorkSource.CorrectiveAction] = [typeof(CompleteCorrectiveAction)],
        [WorkSource.EvidenceRequest] = [typeof(FulfilEvidenceRequest)],
        [WorkSource.RiskTreatmentAction] = [typeof(SubmitRiskTreatmentActionCompletion)],
        [WorkSource.RiskTreatmentActionReview] = [typeof(ReviewRiskTreatmentActionCompletion)],
        [WorkSource.RiskControlTreatmentReview] = [typeof(ReviewRiskControlTreatment)],
        [WorkSource.ControlEvaluationReview] = [typeof(ReviewControlEvaluation)],
        [WorkSource.ControlOperatingPlanApproval] = [typeof(ApproveControlOperatingPlan)],
        [WorkSource.ControlCriterionMappingReview] = [typeof(ReviewControlCriterionMapping)],
        [WorkSource.CriterionApplicabilityReview] = [typeof(ReviewCriterionApplicability)],
        [WorkSource.AccessReviewReview] = [typeof(RecordAccessDecision)],
        [WorkSource.AccessReviewRemediation] = [typeof(RecordAccessRemediationChange)],
        [FindingClosureWork.Kind] = [typeof(CloseFinding)],
        [ControlDecisionWork.DraftReview] = [typeof(ReviewControl)],
        [ControlDecisionWork.DraftApproval] = [typeof(ApproveControl)],
        [ControlDecisionWork.RetirementReview] = [typeof(ReviewControl)],
        [ControlDecisionWork.RetirementApproval] = [typeof(RetireControl)],
        [PolicyDecisionWork.DraftReview] = [typeof(ReviewPolicyDraft)],
        [PolicyDecisionWork.DraftApproval] = [typeof(ApprovePolicy)],
        [PolicyDecisionWork.RetirementReview] = [typeof(ReviewPolicyDraft)],
        [PolicyDecisionWork.RetirementApproval] = [typeof(ApprovePolicyRetirement)],
        [PolicyDecisionWork.PeriodicReview] = [typeof(ConfirmPolicyReview)],
        [BoundaryDecisionWork.Review] = [typeof(ReviewBoundary)],
        [BoundaryDecisionWork.Approval] = [typeof(ApproveBoundary)],
        [CommitmentDecisionWork.Review] = [typeof(ReviewCommitmentDraft)],
        [CommitmentDecisionWork.Approval] = [typeof(ApproveCommitmentDraft)],
        [RiskAcceptanceWork.Kind] = [typeof(AcceptRisk)],
        [PolicyCampaignWork.Acknowledgement] = [typeof(AcknowledgePolicy)],
        [PolicyCampaignWork.TrainingCompletion] = [typeof(RecordTrainingCompletion)]
    };

    internal static bool IsManagementMutation(WorkCandidate candidate) =>
        SourceRequests.TryGetValue(candidate.Kind, out var requests) &&
        requests.Any(typeof(IClientManagementMutationRequest).IsAssignableFrom);
}
