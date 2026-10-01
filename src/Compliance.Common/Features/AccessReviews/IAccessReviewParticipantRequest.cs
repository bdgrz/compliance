using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An access-review request that an assigned reviewer may send without the access-review
///     management permission. The handler enforces the record-level reviewer assignment.
/// </summary>
public interface IAccessReviewParticipantRequest : IAccessReviewRequest;
