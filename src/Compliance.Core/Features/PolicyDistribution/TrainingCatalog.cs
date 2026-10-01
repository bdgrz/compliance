using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A program's training requirements and their immutable versions. A program holds few
///     requirements, so one catalog stream keeps identifiers unique and lists consistent.
/// </summary>
public sealed class TrainingCatalog : Aggregate
{
    public const string Cadence = "hire_within_30_days_then_annual";
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Dictionary<Uuid, List<TrainingRequirementView>> _requirements = [];
    readonly Dictionary<Uuid, Uuid> _defineRequests = [];

    public TrainingCatalog(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "training-catalogs",
            programId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<TrainingRequirementDefined>(ev =>
        {
            if (!_requirements.TryGetValue(ev.RequirementId, out var versions))
            {
                versions = [];
                _requirements.Add(ev.RequirementId, versions);
                _defineRequests[ev.RequirementId] = ev.DefineRequestId;
            }
            versions.Add(new TrainingRequirementView(ev.TenantId, ev.ProgramId,
                ev.RequirementId, ev.Identifier, ev.Version, ev.Version, ev.Content,
                ev.ContentSha256, Cadence, ev.Actor, ev.ChangedAt));
        });
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            "training:" + NormalizeIdentifier(identifier));

    public TrainingRequirementView? Find(Uuid requirementId, long? version = null)
    {
        if (!_requirements.TryGetValue(requirementId, out var versions))
            return null;
        var latest = versions[^1].Version;
        var match = version is { } exact
            ? versions.FirstOrDefault(candidate => candidate.Version == exact)
            : versions[^1];
        return match is null ? null : match with { LatestVersion = latest };
    }

    public IReadOnlyList<TrainingRequirementView> Latest() => _requirements.Values
        .Select(static versions => versions[^1])
        .OrderBy(static requirement => requirement.Identifier, StringComparer.Ordinal)
        .ToArray();

    public Result<TrainingRequirementRegistration> Define(Uuid requestId, string identifier,
        TrainingRequirementContent content, ActorReference actor, DateTimeOffset changedAt)
    {
        var normalized = NormalizeIdentifier(identifier);
        if (normalized.Length is < 1 or > 80 || !normalized.All(static c =>
                char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return Failure("A training requirement identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        if (Validate(content) is { } error)
            return Failure(error);
        var clean = Clean(content);
        var requirementId = IdFor(_tenantId, _programId, normalized);
        if (Find(requirementId) is { } existing)
            return _defineRequests[requirementId] == requestId &&
                   existing.Version == 1 && existing.ContentSha256 == Hash(clean)
                ? Result<TrainingRequirementRegistration>.Success(
                    new TrainingRequirementRegistration(requirementId, normalized, 1))
                : Result<TrainingRequirementRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The training requirement identifier already exists."));
        RaiseEvent(new TrainingRequirementDefined(_tenantId, _programId, requirementId,
            normalized, 1, clean, Hash(clean), requestId, actor, changedAt));
        return Result<TrainingRequirementRegistration>.Success(
            new TrainingRequirementRegistration(requirementId, normalized, 1));
    }

    public Result<TrainingRequirementRegistration> Revise(Uuid requestId, Uuid requirementId,
        long expectedVersion, TrainingRequirementContent content, ActorReference actor,
        DateTimeOffset changedAt)
    {
        if (Find(requirementId) is not { } current)
            return Result<TrainingRequirementRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The training requirement was not found."));
        if (expectedVersion != current.Version)
            return Result<TrainingRequirementRegistration>.Failure(VersionedRecordRules
                .StaleRevision("training requirement", current.Version).ToRequestError());
        if (Validate(content) is { } error)
            return Failure(error);
        var clean = Clean(content);
        RaiseEvent(new TrainingRequirementDefined(_tenantId, _programId, requirementId,
            current.Identifier, current.Version + 1, clean, Hash(clean), requestId, actor,
            changedAt));
        return Result<TrainingRequirementRegistration>.Success(
            new TrainingRequirementRegistration(requirementId, current.Identifier,
                current.Version + 1));
    }

    static Result<TrainingRequirementRegistration> Failure(string message) =>
        Result<TrainingRequirementRegistration>.Failure(new RequestError(
            RequestErrorKind.Validation, message));

    static string? Validate(TrainingRequirementContent? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.CourseName) ||
            content.CourseName.Trim().Length > 200 || content.Description?.Length > 4000)
            return "A training requirement requires a course name of at most 200 characters and a description of at most 4000.";
        if (content.DeliverySource is not ("manual" or "lms_export"))
            return "The delivery source must be manual or lms_export.";
        return PolicyAudience.Validate(content.AudienceKind, content.AudienceTeams);
    }

    static TrainingRequirementContent Clean(TrainingRequirementContent content) => new(
        content.CourseName.Trim(),
        string.IsNullOrWhiteSpace(content.Description) ? null : content.Description.Trim(),
        content.AudienceKind, PolicyAudience.CleanTeams(content.AudienceTeams),
        content.DeliverySource);

    static string Hash(TrainingRequirementContent content) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content,
            ComplianceCoreJsonContext.Default.TrainingRequirementContent)));
}
