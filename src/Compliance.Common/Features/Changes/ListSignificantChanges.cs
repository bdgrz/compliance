using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.changes.list-significant-changes", 1)]
public sealed record ListSignificantChanges(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<SignificantChangeSummaryView>>, IProgramReadRequest, ICallable;
