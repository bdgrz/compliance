using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.system-description.list-system-description-versions", 1)]
public sealed record ListSystemDescriptionVersions(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<SystemDescriptionVersionView>>, IProgramReadRequest, ICallable;
