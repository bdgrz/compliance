using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Responsibilities;
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
[JsonSerializable(typeof(GetTenantMember))]
[JsonSerializable(typeof(ListMemberResponsibilities))]
[JsonSerializable(typeof(IReadOnlyList<ResponsibilityAssignmentView>))]
[JsonSerializable(typeof(ResponsibilityConflictPreview))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ProblemDetails))]
sealed partial class ComplianceJsonContext : JsonSerializerContext;
