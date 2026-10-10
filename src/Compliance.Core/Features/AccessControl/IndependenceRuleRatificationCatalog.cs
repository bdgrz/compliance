using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Retains personal rule ratifications separately from platform-authored drafts.</summary>
public sealed class IndependenceRuleRatificationCatalog : Aggregate
{
    static readonly Uuid CatalogId = Uuid.Parse("b6330afc-1c97-4500-9c6d-c77006fc0dc6", CultureInfo.InvariantCulture);
    readonly List<IndependenceRuleRatificationView> _ratifications = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, IndependenceRuleRatificationView Response)> _decisions = [];

    public IndependenceRuleRatificationCatalog() : base(CatalogId,
        new EventStreamAddress("bdgrz", "independence-rule-ratifications", CatalogId.ToString())) =>
        On<IndependenceRuleRatificationRecorded>(Apply);

    public long Sequence { get; private set; }
    public IReadOnlyList<IndependenceRuleRatificationView> Ratifications =>
        Array.AsReadOnly(_ratifications.ToArray());
    public IndependenceRuleRatificationView? Active => _ratifications.LastOrDefault();

    public IndependenceRuleRatificationView? ForVersion(long ruleVersion) =>
        _ratifications.SingleOrDefault(item => item.RuleVersion == ruleVersion);

    public Result<IndependenceRuleRatificationView> Ratify(Uuid requestId, Uuid ratificationId,
        long ruleVersion, long observedRuleCatalogSequence, long expectedRatificationSequence,
        string ruleContentDigest, string sourceReference, FirmStaffMemberView currentStaff,
        FirmProfessionalDutyDesignationView duty, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "firm_staff") ||
            ratificationId == Uuid.Empty || ruleVersion <= 0 || observedRuleCatalogSequence <= 0 ||
            !ValidDigest(ruleContentDigest) || !Bounded(sourceReference, 2000) ||
            currentStaff is not { IsActive: true, Revision: > 0 } ||
            duty is not { IsActive: true, Duty: FirmProfessionalDuty.RuleRatifier, TenantId: null, Revision: > 0 } ||
            duty.UserId != currentStaff.UserId || duty.StaffMemberId != currentStaff.StaffMemberId ||
            duty.DirectoryStaffRevision != currentStaff.Revision || actor.Id != currentStaff.UserId.ToString())
            return Failure(RequestErrorKind.Validation,
                "Rule ratification requires an active, explicitly designated professional and exact immutable draft digest.");

        var request = new RatifyIndependenceRuleVersion(ratificationId, ruleVersion,
            observedRuleCatalogSequence, expectedRatificationSequence, ruleContentDigest, sourceReference);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedRatificationSequence != Sequence || ruleVersion <= (_ratifications.LastOrDefault()?.RuleVersion ?? 0) ||
            _ratifications.Any(item => item.RatificationId == ratificationId || item.RuleVersion == ruleVersion) ||
            _ratifications.Count >= 100)
            return Failure(RequestErrorKind.Conflict,
                "Reload the ratification sequence; ratifications are append-only and rule versions advance monotonically.");

        var ratification = new IndependenceRuleRatificationView(ratificationId, ruleVersion,
            observedRuleCatalogSequence, ruleContentDigest, sourceReference, currentStaff.StaffMemberId,
            currentStaff.UserId, currentStaff.Revision, duty.DesignationId, duty.Revision, actor, recordedAt);
        RaiseEvent(new IndependenceRuleRatificationRecorded(requestId, expectedRatificationSequence, ratification));
        return Result<IndependenceRuleRatificationView>.Success(ratification);
    }

    void Apply(IndependenceRuleRatificationRecorded ev)
    {
        var item = ev.Ratification;
        if (ev.ExpectedSequence != Sequence || item.RatificationId == Uuid.Empty || item.RuleVersion <= 0 ||
            item.ObservedRuleCatalogSequence <= 0 || !ValidDigest(item.RuleContentDigest) ||
            !Bounded(item.SourceReference, 2000) || item.StaffMemberId == Uuid.Empty || item.UserId == Uuid.Empty ||
            item.DirectoryStaffRevision <= 0 || item.DutyDesignationId == Uuid.Empty || item.DutyRevision <= 0 ||
            !IndependenceRecordValidation.ValidAttribution(ev.RequestId, item.Actor, item.RecordedAt, "firm_staff") ||
            item.Actor.Id != item.UserId.ToString() || item.RuleVersion <= (_ratifications.LastOrDefault()?.RuleVersion ?? 0) ||
            _ratifications.Any(existing => existing.RatificationId == item.RatificationId ||
                existing.RuleVersion == item.RuleVersion) || _ratifications.Count >= 100)
            throw new InvalidOperationException("A personal rule ratification must retain its exact source and advance the sequence.");

        var request = new RatifyIndependenceRuleVersion(item.RatificationId, item.RuleVersion,
            item.ObservedRuleCatalogSequence, ev.ExpectedSequence, item.RuleContentDigest, item.SourceReference);
        _ratifications.Add(item);
        _decisions.Add(ev.RequestId, (Intent(request), item.Actor, item));
        Sequence++;
    }

    Result<IndependenceRuleRatificationView>? Retry(Uuid requestId, object request, ActorReference actor) =>
        _decisions.TryGetValue(requestId, out var previous)
            ? previous.Intent == Intent(request) && previous.Actor == actor
                ? Result<IndependenceRuleRatificationView>.Success(previous.Response)
                : Failure(RequestErrorKind.Conflict,
                    "The request identity already retains another ratification intent or professional actor.")
            : null;

    static bool ValidDigest(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static Result<IndependenceRuleRatificationView> Failure(RequestErrorKind kind, string message) =>
        Result<IndependenceRuleRatificationView>.Failure(new RequestError(kind, message));
}
