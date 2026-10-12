using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The normalized resulting content shared by revision validation and advisory comparison.</summary>
sealed record ApplicationRevisionContent(string Name, string Purpose, string? OwnerReference,
    string? Classification, Uuid? SystemOwnerPersonId, Uuid? AccessOwnerPersonId,
    bool IsRestricted, string? ApplicationKind, string? Criticality)
{
    public CommandFailure? Validate(bool governed)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200 ||
            string.IsNullOrWhiteSpace(Purpose) || Purpose.Length > 2000 ||
            OwnerReference is { Length: > 2000 } || Classification is { Length: > 200 })
            return CommandFailure.InvalidContent(
                "An application requires a name of at most 200 characters and purpose of at most 2000; owner reference and classification cannot exceed 2000 and 200 characters.");
        if (ApplicationKind is { Length: 0 or > 200 } || Criticality is { Length: 0 or > 200 } ||
            Classification is { Length: 0 } || SystemOwnerPersonId == Uuid.Empty ||
            AccessOwnerPersonId == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "Classification, owners, application kind and criticality cannot be blank; kind and criticality cannot exceed 200 characters.");
        if (governed && (Classification is null || SystemOwnerPersonId is null ||
            AccessOwnerPersonId is null || ApplicationKind is null || Criticality is null))
            return CommandFailure.InvalidContent(
                "An application requires classification, system and access owners, application kind and criticality.");
        return null;
    }
}
