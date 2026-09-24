using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ClientServiceRegistration(Uuid ServiceId);

public sealed record ClientServiceView(Uuid TenantId, Uuid ServiceId, long Revision,
    string Name, string Purpose, string OwnerReference, string Status,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    Uuid? ProgramId = null)
{
    public ActorReference LastChangedBy => ActorReference.ForMember(
        LastChangedByMemberId, LastChangedByDisplay);
}

public sealed record ClientServiceRevisionView(Uuid ServiceId, long Revision,
    string Name, string Purpose, string OwnerReference, string Status,
    string? RetirementRationale, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt, Uuid? ProgramId = null)
{
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

[Discriminator("bdgrz.client-service.create", 2)]
public sealed record CreateClientService(Uuid TenantId, Uuid ProgramId, string Name, string Purpose,
    string OwnerReference) : IRequest<ClientServiceRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.client-service.revise", 1)]
public sealed record ReviseClientService(Uuid TenantId, Uuid ServiceId, long ExpectedRevision,
    string Name, string Purpose, string OwnerReference)
    : IRequest, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.client-service.retire", 1)]
public sealed record RetireClientService(Uuid TenantId, Uuid ServiceId, long ExpectedRevision,
    string Rationale) : IRequest, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.client-service.get", 1)]
public sealed record GetClientService(Uuid TenantId, Uuid ServiceId,
    long? MinimumRevision = null)
    : IRequest<ClientServiceView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.client-service.list", 1)]
public sealed record ListClientServices(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ClientServiceView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.client-service.program.list", 1)]
public sealed record ListProgramClientServices(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ClientServiceView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.client-service.revisions.list", 1)]
public sealed record ListClientServiceRevisions(Uuid TenantId, Uuid ServiceId,
    int? Limit = null, string? Cursor = null, long? MinimumServiceRevision = null)
    : IRequest<Page<ClientServiceRevisionView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.client-service.revision.get", 1)]
public sealed record GetClientServiceRevision(Uuid TenantId, Uuid ServiceId, long Revision)
    : IRequest<ClientServiceRevisionView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.client-service.created", 1)]
public sealed record ClientServiceCreated(Uuid TenantId, Uuid ServiceId, string Name,
    string Purpose, string OwnerReference, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt, Uuid ProgramId = default) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

[Discriminator("bdgrz.client-service.revised", 1)]
public sealed record ClientServiceRevised(Uuid TenantId, Uuid ServiceId, long Revision,
    string Name, string Purpose, string OwnerReference, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

[Discriminator("bdgrz.client-service.retired", 1)]
public sealed record ClientServiceRetired(Uuid TenantId, Uuid ServiceId, long Revision,
    string Rationale, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
