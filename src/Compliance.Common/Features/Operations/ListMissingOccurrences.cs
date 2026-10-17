using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.list-missing-occurrences", 1)]
public sealed record ListMissingOccurrences(Uuid TenantId, Uuid ProgramId, DateOnly FromDate, DateOnly ThroughDate, int? Limit = null, string? Cursor = null)
    : IRequest<Page<MissingOccurrenceView>>, IProgramReadRequest, ICallable;
