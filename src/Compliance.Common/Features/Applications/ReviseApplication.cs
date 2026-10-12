using Cntryl.Portia;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.revise", 1)]
public sealed record ReviseApplication(Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    string Name, string Purpose, string? OwnerReference, string? Classification = null, Uuid? SystemOwnerPersonId = null,
    Uuid? AccessOwnerPersonId = null, bool? IsRestricted = null,
    string? ApplicationKind = null, string? Criticality = null)
    : IRequest, IApplicationInventoryWriteRequest, ICallable
{
    [JsonIgnore]
    public ApplicationRevisionField ClassificationIntent { get; init; }
    [JsonIgnore]
    public ApplicationRevisionField SystemOwnerIntent { get; init; }
    [JsonIgnore]
    public ApplicationRevisionField AccessOwnerIntent { get; init; }
}
