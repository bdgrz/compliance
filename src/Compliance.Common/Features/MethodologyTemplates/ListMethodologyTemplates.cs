using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.methodology-templates.list-methodology-templates", 1)]
public sealed record ListMethodologyTemplates(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<MethodologyTemplateSummaryView>>, IProgramReadRequest, ICallable;
