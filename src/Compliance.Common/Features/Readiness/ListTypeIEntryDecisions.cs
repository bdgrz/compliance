using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Lists the program's Type I entry decisions, newest first.</summary>
[Discriminator("bdgrz.readiness.type_i_entry.list", 1)]
public sealed record ListTypeIEntryDecisions(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<TypeIEntryDecisionView>>, IProgramReadRequest, ICallable;
