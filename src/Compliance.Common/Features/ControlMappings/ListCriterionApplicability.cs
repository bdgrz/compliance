using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Lists a program's criterion applicability decisions, optionally by edition or status.</summary>
[Discriminator("bdgrz.criterion_applicability.list", 1)]
public sealed record ListCriterionApplicability(Uuid TenantId, Uuid ProgramId,
    Uuid? EditionId = null, string? Status = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CriterionApplicabilityView>>, IProgramReadRequest, ICallable;
