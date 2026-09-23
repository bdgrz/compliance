using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

static class BoundaryChangeFailureRequestAdapter
{
    public static AggregateOutcome ToOutcome(BoundaryChangeFailure? failure) =>
        failure is null
            ? AggregateOutcome.Commit(Result.Success)
            : AggregateOutcome.Discard(Result.Failure(ToRequestError(failure)));

    public static AggregateOutcome<BoundaryRegistration> ToRegistrationOutcome(
        BoundaryChangeFailure? failure, BoundaryRegistration registration) =>
        failure is null
            ? AggregateOutcome.Commit(Result<BoundaryRegistration>.Success(registration))
            : AggregateOutcome.Discard(Result<BoundaryRegistration>.Failure(
                ToRequestError(failure)));

    static RequestError ToRequestError(BoundaryChangeFailure failure) => failure.Code switch
    {
        BoundaryChangeFailure.Reason.NotFound => new(RequestErrorKind.NotFound,
            "The boundary was not found."),
        BoundaryChangeFailure.Reason.Immutable => new(RequestErrorKind.Conflict,
            "The approved boundary is immutable. Propose a successor draft."),
        BoundaryChangeFailure.Reason.VersionConflict => failure.Version!.ToRequestError(),
        BoundaryChangeFailure.Reason.InvalidContent => new(RequestErrorKind.Validation,
            failure.Message!),
        BoundaryChangeFailure.Reason.SelfReview => new(RequestErrorKind.Forbidden,
            "A boundary author cannot review their own draft."),
        BoundaryChangeFailure.Reason.InvalidReview => new(RequestErrorKind.Validation,
            "A review requires an outcome and rationale."),
        BoundaryChangeFailure.Reason.MissingDiscardRationale => new(RequestErrorKind.Validation,
            "Discarding a draft requires a rationale."),
        BoundaryChangeFailure.Reason.SelfApproval => new(RequestErrorKind.Forbidden,
            "A boundary author cannot approve their own draft."),
        BoundaryChangeFailure.Reason.UnacceptedReview => new(RequestErrorKind.Conflict,
            "Approval requires the latest accepted review of this draft revision."),
        BoundaryChangeFailure.Reason.MissingApprovalRationale => new(RequestErrorKind.Validation,
            "Approval requires a rationale."),
        BoundaryChangeFailure.Reason.MissingImpactDigest => new(RequestErrorKind.Validation,
            "Approval requires the acknowledged impact preview digest."),
        BoundaryChangeFailure.Reason.EffectiveDateOutOfOrder => new(RequestErrorKind.Validation,
            "A successor must become effective after the previous approved version."),
        BoundaryChangeFailure.Reason.NoApprovedVersion => new(RequestErrorKind.Conflict,
            "The boundary has no approved version."),
        BoundaryChangeFailure.Reason.OpenSuccessor => new(RequestErrorKind.Conflict,
            "The boundary already has an open successor draft."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}
