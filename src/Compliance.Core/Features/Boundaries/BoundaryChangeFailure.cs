using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryChangeFailure
{
    public enum Reason
    {
        NotFound,
        Immutable,
        VersionConflict,
        InvalidContent,
        SelfReview,
        InvalidReview,
        MissingDiscardRationale,
        SelfApproval,
        UnacceptedReview,
        MissingApprovalRationale,
        MissingImpactDigest,
        EffectiveDateOutOfOrder,
        NoApprovedVersion,
        OpenSuccessor,
    }

    public Reason Code { get; }
    public VersionConflict? Version { get; }
    public string? Message { get; }

    BoundaryChangeFailure(Reason code, VersionConflict? version = null, string? message = null)
    {
        Code = code;
        Version = version;
        Message = message;
    }

    public static BoundaryChangeFailure Rule(Reason code) =>
        !Enum.IsDefined(code) || code is Reason.VersionConflict or Reason.InvalidContent
            ? throw new ArgumentOutOfRangeException(nameof(code))
            : new BoundaryChangeFailure(code);

    public static BoundaryChangeFailure ForVersion(VersionConflict conflict) =>
        new(Reason.VersionConflict, conflict ?? throw new ArgumentNullException(nameof(conflict)));

    public static BoundaryChangeFailure InvalidContent(string message) =>
        string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("A content failure needs a message.", nameof(message))
            : new BoundaryChangeFailure(Reason.InvalidContent, message: message);
}
