using System.Text.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance;

[PortiaJsonContext]
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(RegisterDeveloperUser))]
[JsonSerializable(typeof(RegisterOidcUser))]
[JsonSerializable(typeof(RegisteredUserIdentity))]
[JsonSerializable(typeof(UserIdentityRegistered))]
[JsonSerializable(typeof(string))]
sealed partial class ComplianceCoreJsonContext : JsonSerializerContext;
