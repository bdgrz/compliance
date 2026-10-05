using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

[Discriminator("bdgrz.control.evaluation.plan.versions.list", 1)]
public sealed record ListControlEvaluationPlanVersions(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, int Limit = 50, string? Cursor = null)
    : IRequest<Page<ControlEvaluationPlanVersionView>>, IControlOperationRequest, ICallable;
