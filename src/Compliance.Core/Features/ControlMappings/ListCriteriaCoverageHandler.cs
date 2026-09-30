using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Joins the immutable catalog edition with the program's accepted mappings. Only accepted
///     versions count, and a control counts once per entry, so drafts and duplicates cannot
///     inflate coverage. Coverage never states that a criterion is satisfied.
/// </summary>
public sealed class ListCriteriaCoverageHandler(IAggregateReader reader, ICriteriaCatalog catalog)
    : IRequestHandler<ListCriteriaCoverage, Page<CriterionCoverageView>>
{
    public async ValueTask<Result<Page<CriterionCoverageView>>> HandleAsync(
        IRequestContext<ListCriteriaCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CoverageState is not (null or "mapped" or "unmapped"))
            return Failure(RequestErrorKind.Validation,
                "The coverage state filter must be mapped or unmapped.");
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The program was not found.");
        if ((request.EditionId ?? program.CriteriaEditionId) is not { } editionId)
            return Failure(RequestErrorKind.Conflict,
                "The program has not selected a criteria edition.");
        if (catalog.GetEdition(editionId) is null)
            return Failure(RequestErrorKind.NotFound, "The criteria edition was not found.");
        var ledger = await reader.HydrateAsync(new ControlCriterionMappingLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var mappings = ledger.ReadAll().Where(mapping => mapping.EditionId == editionId)
            .ToLookup(static mapping => mapping.CriterionIdentifier, StringComparer.Ordinal);
        var items = catalog.ListEntries(editionId, request.Category, request.Kind, null)
            .Select(entry => Coverage(entry, mappings[entry.Identifier].ToArray()))
            .Where(item => request.CoverageState is null ||
                item.CoverageState == request.CoverageState)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "criteria coverage");
    }

    static CriterionCoverageView Coverage(Criterion entry,
        IReadOnlyList<ControlCriterionMappingView> mappings)
    {
        var mapped = mappings.Where(static mapping => mapping.ActiveVersionNumber is not null)
            .GroupBy(static mapping => mapping.ControlId)
            .Select(static group => group.First())
            .Select(static mapping => new MappedControlReference(mapping.MappingId,
                mapping.ControlId, mapping.ActiveControlVersionId!.Value,
                mapping.ActiveVersionNumber!.Value,
                mapping.Versions[mapping.ActiveVersionNumber.Value - 1].ApplicabilityExplanation))
            .ToArray();
        return new CriterionCoverageView(entry.EditionId, entry.Identifier, entry.Kind,
            entry.Category, entry.ParentIdentifier, entry.Summary,
            mapped.Length > 0 ? "mapped" : "unmapped", mapped,
            mappings.Count(static mapping => mapping.Status == "pending"));
    }

    static Result<Page<CriterionCoverageView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<CriterionCoverageView>>.Failure(new RequestError(kind, message));
}
