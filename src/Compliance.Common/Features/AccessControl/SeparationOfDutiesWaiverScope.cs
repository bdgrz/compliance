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
}

public static class SeparationOfDutiesActions
{
    public const string Review = "review";
    public const string Approve = "approve";
}
