using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The program's versioned M0-D10 method: qualitative 5x5 with optional appetite.</summary>
public sealed class RiskMethod : Aggregate
{
    public const string QualitativeFiveByFive = "qualitative_5x5";
    public const string AnnualReassessment = "P1Y";
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<RiskMethodVersionView> _versions = [];

    public long PublishedVersion => _versions.Count;
    public RiskMethodVersionView? Current => _versions.Count == 0 ? null : _versions[^1];

    public RiskMethod(Uuid tenantId, Uuid programId)
        : base(IdFor(tenantId, programId),
            new EventStreamAddress(tenantId.ToString(), "risk-methods",
                IdFor(tenantId, programId).ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<RiskMethodVersionPublished>(ev => _versions.Add(ev.Version));
    }

    public static Uuid IdFor(Uuid tenantId, Uuid programId) =>
        Uuid.CreateVersion5(tenantId, $"risk-method:{programId}");

    public RiskMethodVersionView? GetVersion(long version) =>
        version >= 1 && version <= _versions.Count ? _versions[(int)version - 1] : null;

    public RiskMethodVersionView? Find(Uuid methodVersionId) =>
        _versions.Find(version => version.MethodVersionId == methodVersionId);

    public CommandFailure? Publish(Uuid programId, long expectedVersion,
        IReadOnlyList<string>? likelihoodScale, IReadOnlyList<string>? impactScale,
        int? appetiteThreshold, ActorReference publishedBy, DateTimeOffset publishedAt)
    {
        if (programId != _programId)
            return CommandFailure.MissingRecord("The risk method was not found.");
        if (expectedVersion != PublishedVersion)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk method",
                PublishedVersion));
        if (!ValidScale(likelihoodScale) || !ValidScale(impactScale))
            return CommandFailure.InvalidContent(
                "A qualitative 5x5 method requires five likelihood and five impact descriptors of at most 200 characters.");
        if (appetiteThreshold is < 1 or > 25)
            return CommandFailure.InvalidContent(
                "The appetite threshold must be a residual score from 1 to 25.");
        var version = PublishedVersion + 1;
        RaiseEvent(new RiskMethodVersionPublished(_tenantId, programId,
            new RiskMethodVersionView(_tenantId, programId,
                Uuid.CreateVersion5(Id, version.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                version, QualitativeFiveByFive, [.. likelihoodScale!.Select(static d => d.Trim())],
                [.. impactScale!.Select(static d => d.Trim())], appetiteThreshold,
                AnnualReassessment, publishedBy, publishedAt)));
        return null;
    }

    static bool ValidScale(IReadOnlyList<string>? scale) =>
        scale is { Count: 5 } && scale.All(static descriptor =>
            !string.IsNullOrWhiteSpace(descriptor) && descriptor.Trim().Length <= 200);
}
