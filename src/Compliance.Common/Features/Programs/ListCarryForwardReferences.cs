using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.programs.list-carry-forward-references", 1)]
public sealed record ListCarryForwardReferences(Uuid TenantId, Uuid ProgramId, Uuid ProgramRolloverId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CarryForwardReferenceView>>, IProgramReadRequest, ICallable;
