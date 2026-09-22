using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

[PortiaJsonContext]
[JsonSerializable(typeof(RegisterTenant))]
[JsonSerializable(typeof(TenantRegistration))]
[JsonSerializable(typeof(RegisterMember))]
[JsonSerializable(typeof(DeleteTeam))]
[JsonSerializable(typeof(UnprotectedCompositionRequest))]
sealed partial class RequestScenarioJsonContext : JsonSerializerContext;
