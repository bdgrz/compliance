using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A client management record mutation, including an assigned reviewer's access decision.</summary>
public interface IAccessReviewMutationRequest : IAccessReviewRequest, IClientManagementMutationRequest;
