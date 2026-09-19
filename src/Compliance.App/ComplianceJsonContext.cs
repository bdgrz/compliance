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
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(Uuid))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ProblemDetails))]
sealed partial class ComplianceJsonContext : JsonSerializerContext;
