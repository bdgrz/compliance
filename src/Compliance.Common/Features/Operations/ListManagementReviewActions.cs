using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.operations.list-management-review-actions", 1)]
public sealed record ListManagementReviewActions(Uuid TenantId, Uuid ProgramId, Uuid ManagementReviewId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ManagementReviewActionView>>, IProgramReadRequest, ICallable;
