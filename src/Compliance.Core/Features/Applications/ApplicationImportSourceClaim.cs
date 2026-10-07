namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An accepted source observation, distinct from the governed application fields.</summary>
public sealed record ApplicationImportSourceClaim(ApplicationImportPlanStarted Plan,
    ApplicationImportPlannedRow Observation, DateTimeOffset CommittedAt)
{
    public IReadOnlyList<string> ChangedFields(ApplicationImportStagedRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var changed = new List<string>();
        if (Observation.Name.Trim() != row.Name?.Trim())
            changed.Add("name");
        if (Observation.Purpose.Trim() != row.Purpose?.Trim())
            changed.Add("purpose");
        if (Observation.OwnerReference?.Trim() != row.OwnerReference?.Trim())
            changed.Add("owner_reference");
        return changed.AsReadOnly();
    }
}
