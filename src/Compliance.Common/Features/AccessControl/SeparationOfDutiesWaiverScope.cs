using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Identifies the exact conflict a separation-of-duties waiver may cover.</summary>
public sealed record SeparationOfDutiesWaiverScope(
    string RecordType,
    Uuid RecordId,
    Uuid VersionId,
    long Revision,
    string Action);

public static class SeparationOfDutiesRecordTypes
{
    public const string Boundary = "boundary";

    /// <summary>An access-review scope decision; the version is the instance ID.</summary>
    public const string SystemInstanceAccessReviewScope = "system_instance_access_review_scope";
}

public static class SeparationOfDutiesActions
{
    public const string Review = "review";
    public const string Approve = "approve";
}
