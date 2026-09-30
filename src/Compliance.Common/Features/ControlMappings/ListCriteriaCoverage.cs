using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Lists each catalog entry of an edition (the program's selected edition by default) with
///     the controls whose accepted mappings address it. Coverage is a mapping fact only.
/// </summary>
[Discriminator("bdgrz.control_mappings.coverage.list", 1)]
public sealed record ListCriteriaCoverage(Uuid TenantId, Uuid ProgramId,
    Uuid? EditionId = null, string? Category = null, string? Kind = null,
    string? CoverageState = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CriterionCoverageView>>, IProgramReadRequest, ICallable;
