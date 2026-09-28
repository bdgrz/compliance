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
[JsonSerializable(typeof(ListResponsibilities))]
[JsonSerializable(typeof(PreviewResponsibilityConflicts))]
[JsonSerializable(typeof(ResponsibilitySetView))]
[JsonSerializable(typeof(ResponsibilityConflictPreview))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ProblemDetails))]
sealed partial class ComplianceJsonContext : JsonSerializerContext;
