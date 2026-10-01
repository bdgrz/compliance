using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Joins the immutable catalog edition with the program's projected accepted mappings and
///     not-applicable decisions. Only accepted versions count, and a control counts once per
///     entry, so drafts and duplicates cannot inflate coverage. A mapping to a control version
///     that is no longer current is flagged for remapping. Coverage never states that a
///     criterion is satisfied.
/// </summary>
public sealed class ListCriteriaCoverageHandler(IAggregateReader reader, ICriteriaCatalog catalog,
    CriteriaCoverageReadConsistency coverage)
    : IRequestHandler<ListCriteriaCoverage, Page<CriterionCoverageView>>
{
    public async ValueTask<Result<Page<CriterionCoverageView>>> HandleAsync(
        IRequestContext<ListCriteriaCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CoverageState is not (null or "mapped" or "unmapped" or "not_applicable"))
            return Failure(RequestErrorKind.Validation,
                "The coverage state filter must be mapped, unmapped, or not_applicable.");
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The program was not found.");
        if ((request.EditionId ?? program.CriteriaEditionId) is not { } editionId)
            return Failure(RequestErrorKind.Conflict,
                "The program has not selected a criteria edition.");
        if (catalog.GetEdition(editionId) is null)
            return Failure(RequestErrorKind.NotFound, "The criteria edition was not found.");
        var projected = await coverage.ReadMappingsAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(projected.Error);
        var decisions = await coverage.ReadDecisionsAsync(request.TenantId, request.ProgramId,
            ct).ConfigureAwait(false);
        if (!decisions.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(decisions.Error);
        var editionMappings = projected.Value.Where(mapping => mapping.EditionId == editionId)
            .ToArray();
        var current = new Dictionary<Uuid, Uuid?>();
        foreach (var controlId in editionMappings
                     .Where(static mapping => mapping.ActiveVersionNumber is not null)
                     .Select(static mapping => mapping.ControlId).Distinct())
        {
            var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
                controlId), ct).ConfigureAwait(false);
            current[controlId] = control.ApprovedVersion is { Status: "approved" } version
                ? version.VersionId
                : null;
        }
        var mappings = editionMappings.ToLookup(static mapping => mapping.CriterionIdentifier,
            StringComparer.Ordinal);
        var notApplicable = decisions.Value
            .Where(decision => decision.EditionId == editionId &&
                decision.Status == "not_applicable")
            .ToDictionary(static decision => decision.CriterionIdentifier,
                static decision => decision.DecisionId, StringComparer.Ordinal);
        var items = catalog.ListEntries(editionId, request.Category, request.Kind, null)
            .Select(entry => Coverage(entry, mappings[entry.Identifier].ToArray(), current,
                notApplicable.TryGetValue(entry.Identifier, out var decisionId)
                    ? decisionId
                    : null))
            .Where(item => request.CoverageState is null ||
                item.CoverageState == request.CoverageState)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "criteria coverage");
    }

    static CriterionCoverageView Coverage(Criterion entry,
        IReadOnlyList<ControlCriterionMappingView> mappings,
        IReadOnlyDictionary<Uuid, Uuid?> currentVersions, Uuid? notApplicableDecisionId)
    {
        var mapped = mappings.Where(static mapping => mapping.ActiveVersionNumber is not null)
            .OrderBy(static mapping => mapping.MappingId.ToString(), StringComparer.Ordinal)
            .GroupBy(static mapping => mapping.ControlId)
            .Select(static group => group.First())
            .Select(mapping => new MappedControlReference(mapping.MappingId,
                mapping.ControlId, mapping.ActiveControlVersionId!.Value,
                mapping.ActiveVersionNumber!.Value,
                mapping.Versions[mapping.ActiveVersionNumber.Value - 1].ApplicabilityExplanation)
            {
                RemapRequired = currentVersions.GetValueOrDefault(mapping.ControlId) !=
                                mapping.ActiveControlVersionId,
            })
            .ToArray();
        var state = notApplicableDecisionId is not null ? "not_applicable"
            : mapped.Length > 0 ? "mapped"
            : "unmapped";
        return new CriterionCoverageView(entry.EditionId, entry.Identifier, entry.Kind,
            entry.Category, entry.ParentIdentifier, entry.Summary, state, mapped,
            mappings.Count(static mapping => mapping.Status == "pending"))
        {
            NotApplicableDecisionId = notApplicableDecisionId,
        };
    }

    static Result<Page<CriterionCoverageView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<CriterionCoverageView>>.Failure(new RequestError(kind, message));
}
