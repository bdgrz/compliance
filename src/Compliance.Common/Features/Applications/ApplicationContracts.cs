using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationInventoryRequest : IRequestBase
{
    Uuid TenantId { get; }
}

public sealed record ApplicationRegistration(Uuid ApplicationId);
public sealed record SystemInstanceRegistration(Uuid SystemInstanceId);

/// <summary>A tenant-authored declaration. References do not establish a verified owner or classification.</summary>
public sealed record ApplicationView(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    string SourceKind, string SourceIdentifier, bool HasSystemInstances,
    IReadOnlyList<string> Unresolved, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt);

/// <summary>An immutable tenant-authored application state after one aggregate event.</summary>
public sealed record ApplicationRevisionView(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    string SourceKind, string SourceIdentifier, bool HasSystemInstances,
    IReadOnlyList<string> Unresolved, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    string ChangeKind, Uuid? SystemInstanceId, SystemInstanceView? SystemInstance);

/// <summary>A declared concrete application boundary, not an access-review inclusion decision.</summary>
public sealed record SystemInstanceView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, string Name, string Kind, string? AccessBoundaryReference,
    string SourceKind, string? SourceIdentifier, IReadOnlyList<string> Unresolved,
    Uuid DeclaredByMemberId,
    string DeclaredByDisplay, DateTimeOffset DeclaredAt);

/// <summary>
/// A governed reference in the current draft or an approved boundary version. Historical draft
/// revisions are not retained here; the boundary event history remains their source.
/// </summary>
public sealed record ApplicationBoundaryReferenceView(Uuid TenantId, string SubjectType,
    Uuid GovernedRecordId, Uuid BoundaryId, Uuid ProgramId, Uuid VersionId,
    Uuid EntryId, long Revision, string Status, DateOnly? EffectiveFrom,
    string Kind, string Subject, string OwnerReference, string Rationale);

[Discriminator("bdgrz.application.declare", 1)]
public sealed record DeclareApplication(Uuid TenantId, string Name, string Purpose,
    string? OwnerReference = null)
    : IRequest<ApplicationRegistration>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.revise", 1)]
public sealed record ReviseApplication(Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    string Name, string Purpose, string? OwnerReference)
    : IRequest, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.system_instance.declare", 1)]
public sealed record DeclareSystemInstance(Uuid TenantId, Uuid ApplicationId,
    long ExpectedApplicationRevision, string Name, string Kind,
    string? AccessBoundaryReference = null, string? SourceIdentifier = null)
    : IRequest<SystemInstanceRegistration>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.get", 1)]
public sealed record GetApplication(Uuid TenantId, Uuid ApplicationId,
    long? MinimumRevision = null)
    : IRequest<ApplicationView>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.list", 1)]
public sealed record ListApplications(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationView>>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.revision.get", 1)]
public sealed record GetApplicationRevision(Uuid TenantId, Uuid ApplicationId, long Revision)
    : IRequest<ApplicationRevisionView>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.revision.list", 1)]
public sealed record ListApplicationRevisions(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumApplicationRevision = null)
    : IRequest<Page<ApplicationRevisionView>>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.system_instance.get", 1)]
public sealed record GetSystemInstance(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId)
    : IRequest<SystemInstanceView>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.system_instance.list", 1)]
public sealed record ListSystemInstances(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<SystemInstanceView>>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved references, after source catchup.</summary>
public sealed record ListApplicationBoundaryReferences(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.system_instance.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved references, after source catchup.</summary>
public sealed record ListSystemInstanceBoundaryReferences(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, IApplicationInventoryRequest, ICallable;

[Discriminator("bdgrz.application.declared", 1)]
public sealed record ApplicationDeclared(Uuid TenantId, Uuid ApplicationId,
    string Name, string Purpose, string? OwnerReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.application.revised", 1)]
public sealed record ApplicationRevised(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.system_instance.declared", 1)]
public sealed record SystemInstanceDeclared(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ApplicationRevision, string Name, string Kind,
    string? AccessBoundaryReference, string? SourceIdentifier,
    Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent;
