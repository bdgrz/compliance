using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc;

namespace Bdgrz.Compliance;

[PortiaJsonContext]
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ComplianceAuthenticationClientConfiguration))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(Uuid))]
[JsonSerializable(typeof(IReadOnlyList<Uuid>))]
[JsonSerializable(typeof(AssignResponsibility))]
[JsonSerializable(typeof(AcceptServiceEngagement))]
[JsonSerializable(typeof(RatifyIndependenceRuleVersion))]
[JsonSerializable(typeof(IndependenceRuleRatificationView))]
[JsonSerializable(typeof(RecordPartnerIndependenceEvaluation))]
[JsonSerializable(typeof(GetServiceEngagementPartnerReviewContext))]
[JsonSerializable(typeof(PartnerIndependenceEvaluationView))]
[JsonSerializable(typeof(ServiceEngagementPartnerReviewContextView))]
[JsonSerializable(typeof(RecordFirmProfessionalDutyDesignation))]
[JsonSerializable(typeof(RevokeFirmProfessionalDutyDesignation))]
[JsonSerializable(typeof(GetFirmProfessionalDutyCatalog))]
[JsonSerializable(typeof(FirmProfessionalDutyDesignationView))]
[JsonSerializable(typeof(FirmProfessionalDutyCatalogView))]
[JsonSerializable(typeof(RevokeServiceEngagementActualStaff))]
[JsonSerializable(typeof(RevokeResponsibility))]
[JsonSerializable(typeof(GrantAccess))]
[JsonSerializable(typeof(RevokeAccessGrant))]
[JsonSerializable(typeof(ListAccessGrants))]
[JsonSerializable(typeof(AccessGrantProposal))]
[JsonSerializable(typeof(AccessGrantPrincipal))]
[JsonSerializable(typeof(AccessGrantScope))]
[JsonSerializable(typeof(AccessGrantSource))]
[JsonSerializable(typeof(AccessGrantSetView))]
[JsonSerializable(typeof(AccessGrantView))]
[JsonSerializable(typeof(ListResponsibilities))]
[JsonSerializable(typeof(PreviewResponsibilityConflicts))]
[JsonSerializable(typeof(ResponsibilitySetView))]
[JsonSerializable(typeof(TenantMembershipView))]
[JsonSerializable(typeof(SuspendMember))]
[JsonSerializable(typeof(ReinstateMember))]
[JsonSerializable(typeof(DeprovisionMember))]
[JsonSerializable(typeof(GetTenantMember))]
[JsonSerializable(typeof(ListMemberResponsibilities))]
[JsonSerializable(typeof(IReadOnlyList<ResponsibilityAssignmentView>))]
[JsonSerializable(typeof(ResponsibilityConflictPreview))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(PersonalContactDetails))]
[JsonSerializable(typeof(Bdgrz.Compliance.Features.Criteria.CriteriaTextOverlayContent))]
sealed partial class ComplianceJsonContext : JsonSerializerContext;
