using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A tenant-scoped access-review request; it requires the access-review permission.</summary>
public interface IAccessReviewRequest : IRequestBase
{
    Uuid TenantId { get; }
}
