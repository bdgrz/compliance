using System.Text.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc;

namespace Bdgrz.Compliance;

[PortiaJsonContext]
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ComplianceAuthenticationClientConfiguration))]
[JsonSerializable(typeof(ContinueWithDeveloperIdentity))]
[JsonSerializable(typeof(ContinueWithOidcProvider))]
[JsonSerializable(typeof(AuthenticatedUserIdentity))]
[JsonSerializable(typeof(RegisterTenant))]
[JsonSerializable(typeof(TenantRegistration))]
[JsonSerializable(typeof(RequestTenantSlugSurrender))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ProblemDetails))]
sealed partial class ComplianceJsonContext : JsonSerializerContext;
