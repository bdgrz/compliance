using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.change.preview", 1)]
public sealed record PreviewApplicationChange(Uuid TenantId, Uuid ApplicationId,
    long ExpectedApplicationRevision, string ChangeKind, string? Name = null,
    string? Purpose = null, string? OwnerReference = null, string? Classification = null,
    Uuid? SystemOwnerPersonId = null, Uuid? AccessOwnerPersonId = null,
    string? ApplicationKind = null, string? Criticality = null)
    : IRequest<ApplicationChangePreview>, IRestrictedApplicationResourceReadRequest,
        IProgramManagementRequest, ICallable
{
    [JsonIgnore]
    public ApplicationRevisionField ClassificationIntent { get; init; }
    [JsonIgnore]
    public ApplicationRevisionField SystemOwnerIntent { get; init; }
    [JsonIgnore]
    public ApplicationRevisionField AccessOwnerIntent { get; init; }
}
