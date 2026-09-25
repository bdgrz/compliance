using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>
/// Identifier is the stable catalog key. SourceIdentifier is null for a point of focus
/// because the 2022 source does not number those points.
/// </summary>
public sealed record Criterion(Uuid EditionId, string Identifier, string? SourceIdentifier,
    string Category, string Kind, string? ParentIdentifier, string Summary);
