using System.Text.RegularExpressions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public interface ICriteriaCatalog
{
    IReadOnlyList<CriteriaCatalogEdition> Editions { get; }
    CriteriaCatalogEdition? GetEdition(Uuid editionId);
    Criterion? GetEntry(Uuid editionId, string identifier);
    IReadOnlyList<Criterion> ListEntries(Uuid editionId, string? category, string? kind,
        string? parentIdentifier);
}
