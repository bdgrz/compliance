using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed record CriteriaCatalogEdition(Uuid EditionId, string Framework,
    string EditionLabel, DateTimeOffset PublishedAt, bool IsComplete,
    string CoverageNote, string SourceUrl);

/// <summary>
/// Identifier is the stable catalog key. SourceIdentifier is null for a point of focus
/// because the 2022 source does not number those points.
/// </summary>
public sealed record Criterion(Uuid EditionId, string Identifier, string? SourceIdentifier,
    string Category, string Kind, string? ParentIdentifier, string Summary);

[Discriminator("bdgrz.criteria.editions.list", 1)]
public sealed record ListCriteriaCatalogEditions(Uuid TenantId)
    : IRequest<IReadOnlyList<CriteriaCatalogEdition>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.criteria.edition.get", 1)]
public sealed record GetCriteriaCatalogEdition(Uuid TenantId, Uuid EditionId)
    : IRequest<CriteriaCatalogEdition>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.criteria.entries.list", 1)]
public sealed record ListCriteriaCatalogEntries(Uuid TenantId, Uuid EditionId,
    string? Category = null, string? Kind = null, string? ParentIdentifier = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<Criterion>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.criteria.entry.get", 1)]
public sealed record GetCriteriaCatalogEntry(Uuid TenantId, Uuid EditionId,
    string Identifier) : IRequest<Criterion>, ITenantAccessRequest, ICallable;
