using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record SeparationOfDutiesWaiverView(
    Uuid TenantId,
    Uuid WaiverId,
    SeparationOfDutiesWaiverScope Scope,
    Uuid BeneficiaryMemberId,
    Uuid RequesterMemberId,
    string RequesterDisplay,
    string Rationale,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt,
    Uuid? ApproverMemberId,
    string? ApproverDisplay,
    DateTimeOffset? ApprovedAt,
    string Status,
    bool Active)
{
    readonly ActorReference? _requester;
    readonly ActorReference? _approver;

    [JsonPropertyName("requester")]
    public ActorReference Requester
    {
        get => _requester ?? ActorReference.ForMember(RequesterMemberId, RequesterDisplay);
        init => _requester = value;
    }

    [JsonPropertyName("approver")]
    public ActorReference? Approver
    {
        get => ApproverMemberId is { } memberId && ApproverDisplay is { } display
            ? _approver ?? ActorReference.ForMember(memberId, display)
            : null;
        init => _approver = value;
    }
}
