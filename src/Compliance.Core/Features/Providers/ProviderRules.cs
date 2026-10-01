using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>M0-D11 declarations and the ordinary citation metadata boundary.</summary>
public static class ProviderRules
{
    public static string? InputError(ProviderContent? content) =>
        content is null || content.Dependencies?.Any(static dependency => dependency is null) == true ||
        content.MaterialityBasis?.Any(static basis => basis is null) == true
            ? "A provider declaration requires content with non-null collection entries."
            : null;

    public static ProviderContent Normalize(ProviderContent content) => content with
    {
        Name = content.Name?.Trim() ?? string.Empty,
        ProviderKind = content.ProviderKind?.Trim() ?? string.Empty,
        Materiality = Optional(content.Materiality),
        MaterialityBasis = Array.AsReadOnly((content.MaterialityBasis ?? [])
            .Select(static basis => basis?.Trim() ?? string.Empty).Order(StringComparer.Ordinal).ToArray()),
        MaterialityRationale = Optional(content.MaterialityRationale),
        BoundaryTreatment = content.Subservice ? Optional(content.BoundaryTreatment) ?? "carve_out" : Optional(content.BoundaryTreatment),
        BoundaryTreatmentRationale = Optional(content.BoundaryTreatmentRationale),
        OwnerReference = Optional(content.OwnerReference),
        SourceCitation = Normalize(content.SourceCitation),
        Dependencies = Array.AsReadOnly((content.Dependencies ?? []).Select(static dependency => dependency with
        {
            SubjectKind = dependency.SubjectKind?.Trim() ?? string.Empty,
            Rationale = dependency.Rationale?.Trim() ?? string.Empty,
            UnresolvedReference = Optional(dependency.UnresolvedReference),
            SourceCitation = Normalize(dependency.SourceCitation),
        }).ToArray()),
    };

    public static string? Validate(ProviderContent content)
    {
        if (content.Name.Length is < 1 or > 200 || content.ProviderKind.Length is < 1 or > 200)
            return "A provider requires a name and kind of at most 200 characters.";
        if (content.Materiality is not (null or "material" or "not_material") ||
            content.MaterialityBasis!.Any(static basis => basis is not ("customer_data" or "critical_path")) ||
            content.MaterialityBasis!.Distinct(StringComparer.Ordinal).Count() != content.MaterialityBasis!.Count ||
            content.Materiality == "not_material" && content.MaterialityBasis!.Count > 0)
            return "Materiality must follow customer_data or critical_path exposure; spend is not a basis.";
        if (content.MaterialityRationale is { Length: > 2000 } ||
            content.BoundaryTreatmentRationale is { Length: > 2000 } || content.OwnerReference is { Length: > 200 })
            return "Provider rationale or owner reference exceeds its bound.";
        if (content.Subservice ? content.BoundaryTreatment is not ("carve_out" or "inclusive") ||
                content.BoundaryTreatment == "inclusive" && content.BoundaryTreatmentRationale is null
            : content.BoundaryTreatment is not null || content.BoundaryTreatmentRationale is not null)
            return "A subservice defaults to carve_out; inclusive requires an authored rationale.";
        if (content.OwnerPersonId == Uuid.Empty || content.Dependencies!.Count > 100)
            return "Provider references must be nonempty and bounded to 100 dependencies.";
        if (CitationError(content.SourceCitation) is { } citationError)
            return citationError;
        foreach (var dependency in content.Dependencies!)
        {
            if (dependency.SubjectKind is not ("client_service" or "system_instance") ||
                dependency.SubjectId == Uuid.Empty || dependency.ProgramId == Uuid.Empty || dependency.ApplicationId == Uuid.Empty ||
                dependency.Rationale.Length is < 1 or > 2000 || dependency.UnresolvedReference is { Length: > 1000 } ||
                dependency.EffectiveUntilExclusive <= dependency.EffectiveFrom ||
                (dependency.SubjectId is null ? dependency.UnresolvedReference is null || dependency.ProgramId is not null || dependency.ApplicationId is not null
                    : dependency.SubjectKind == "client_service" ? dependency.ProgramId is null || dependency.ApplicationId is not null
                    : dependency.ApplicationId is null || dependency.ProgramId is not null))
                return "A dependency requires a canonical subject and owning context, or an unresolved reference, rationale and a valid half-open interval.";
            if (CitationError(dependency.SourceCitation) is { } error)
                return error;
        }
        return null;
    }

    public static IReadOnlyList<string> Unresolved(ProviderContent content)
    {
        var unresolved = new List<string>();
        if (content.Materiality is null || content.MaterialityRationale is null ||
            content.Materiality == "material" && content.MaterialityBasis?.Count is not > 0)
            unresolved.Add("materiality");
        if (content.OwnerPersonId is null)
            unresolved.Add("owner");
        if (content.SourceCitation is null)
            unresolved.Add("source_citation");
        if (content.Subservice && content.BoundaryTreatment == "carve_out")
            unresolved.Add("csocs");
        for (var index = 0; index < (content.Dependencies?.Count ?? 0); index++)
        {
            if (content.Dependencies![index].SubjectId is null)
                unresolved.Add($"dependencies[{index}].subject");
            if (content.Dependencies[index].SourceCitation is null)
                unresolved.Add($"dependencies[{index}].source_citation");
        }
        return unresolved.AsReadOnly();
    }

    static string? CitationError(ProviderSourceCitation? citation) => citation is null ? null :
        citation.MetadataClassification is not ("public" or "internal") ||
        citation.ArtifactKind.Length is < 1 or > 200 || citation.Title.Length is < 1 or > 200 ||
        citation.VersionOrDate.Length is < 1 or > 200 || citation.Locator.Length is < 1 or > 1000 ||
        citation.ArtifactId == Uuid.Empty
            ? "The citation requires bounded ordinary metadata explicitly classified public or internal; sensitive metadata belongs to due diligence."
            : null;

    static ProviderSourceCitation? Normalize(ProviderSourceCitation? citation) => citation is null ? null : citation with
    {
        ArtifactKind = citation.ArtifactKind?.Trim() ?? string.Empty,
        Title = citation.Title?.Trim() ?? string.Empty,
        VersionOrDate = citation.VersionOrDate?.Trim() ?? string.Empty,
        Locator = citation.Locator?.Trim() ?? string.Empty,
        MetadataClassification = citation.MetadataClassification?.Trim() ?? string.Empty,
    };

    static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
