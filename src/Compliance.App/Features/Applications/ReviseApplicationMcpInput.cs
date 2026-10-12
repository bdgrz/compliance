using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.revise.mcp-input", 1)]
public sealed class ReviseApplicationMcpInput : IRequest, IApplicationInventoryWriteRequest, ICallable
{
    string? _classification;
    Uuid? _systemOwnerPersonId;
    Uuid? _accessOwnerPersonId;

    public required Uuid TenantId { get; init; }
    public required Uuid ApplicationId { get; init; }
    public required long ExpectedRevision { get; init; }
    public required string Name { get; init; }
    public required string Purpose { get; init; }
    public required string? OwnerReference { get; init; }
    // Regular setters preserve omission; generated init-only construction also assigns absent members.
    public string? Classification
    {
        get => _classification;
        set { _classification = value; ClassificationIntent = new(true); }
    }
    public Uuid? SystemOwnerPersonId
    {
        get => _systemOwnerPersonId;
        set { _systemOwnerPersonId = value; SystemOwnerIntent = new(true); }
    }
    public Uuid? AccessOwnerPersonId
    {
        get => _accessOwnerPersonId;
        set { _accessOwnerPersonId = value; AccessOwnerIntent = new(true); }
    }
    public bool? IsRestricted { get; init; }
    public string? ApplicationKind { get; init; }
    public string? Criticality { get; init; }

    [JsonIgnore]
    public ApplicationRevisionField ClassificationIntent { get; private set; }
    [JsonIgnore]
    public ApplicationRevisionField SystemOwnerIntent { get; private set; }
    [JsonIgnore]
    public ApplicationRevisionField AccessOwnerIntent { get; private set; }
}
