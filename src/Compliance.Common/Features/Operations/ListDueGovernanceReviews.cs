using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.list-due-governance-reviews", 1)]
public sealed record ListDueGovernanceReviews(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<GovernanceReviewDueView>>, IProgramReadRequest, ICallable;
