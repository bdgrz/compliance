using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>
/// An immutable platform edition. ContentRights states what the platform ships
/// (identifiers and original summaries only); SupportGaps disclose category-level limits.
/// </summary>
public sealed record CriteriaCatalogEdition(Uuid EditionId, string Framework,
    string EditionLabel, DateTimeOffset PublishedAt, bool IsComplete,
    string CoverageNote, string SourceUrl, string ContentRights,
    IReadOnlyList<CriteriaSupportGap> SupportGaps);
