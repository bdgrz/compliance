using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Platform content only: attributable draft classifications, never client facts or ratification.</summary>
public sealed class IndependenceRuleCatalog : Aggregate
{
    static readonly Uuid CatalogId = Uuid.Parse("f53d539b-6680-510b-9cfb-127b311fa66c", CultureInfo.InvariantCulture);
    readonly List<IndependenceRuleVersionView> _rules = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, IndependenceRuleVersionView Response)> _decisions = [];

    public IndependenceRuleCatalog() : base(CatalogId,
        new EventStreamAddress("bdgrz", "independence-rule-catalog", CatalogId.ToString()))
    {
        On<IndependenceRulesRevised>(Apply);
    }

    public long Sequence => _rules.Count;
    public IReadOnlyList<IndependenceRuleVersionView> Versions => Array.AsReadOnly(_rules.ToArray());
    public IndependenceRuleVersionView? Current => _rules.LastOrDefault();

    public Result<IndependenceRuleVersionView> ReviseRules(Uuid requestId, long expectedSequence,
        IndependenceRuleContent content, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator") || !IndependenceRecordValidation.ValidRules(content))
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Validation,
                "Draft rules require bounded classified services, a look-back of at least twelve months and attributed source facts.");
        content = content with { ServiceRules = Array.AsReadOnly(content.ServiceRules.ToArray()) };
        var request = new ReviseIndependenceRules(expectedSequence, content);
        if (Retry<IndependenceRuleVersionView>(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _rules.Count >= 100)
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Conflict,
                "Reload the independence sequence; at most one hundred immutable rule versions are supported.");
        var version = new IndependenceRuleVersionView(_rules.Count + 1, content, actor, recordedAt, false);
        var revised = new IndependenceRulesRevised(requestId, expectedSequence, version);
        if (!Fits(revised))
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Validation, "The rule event exceeds the bounded payload.");
        RaiseEvent(revised);
        return Result<IndependenceRuleVersionView>.Success(version);
    }

    void Apply(IndependenceRulesRevised revised)
    {
        if (revised.ExpectedSequence != Sequence || revised.Version.Version != Sequence + 1 ||
            revised.Version.IsRatified || !IndependenceRecordValidation.ValidRules(revised.Version.Content))
            throw new InvalidOperationException("Draft rule versions must be consecutive, valid and cannot claim ratification.");
        var version = revised.Version with
        {
            Content = revised.Version.Content with
            {
                ServiceRules = Array.AsReadOnly(revised.Version.Content.ServiceRules.ToArray())
            }
        };
        _rules.Add(version);
        _decisions.Add(revised.RequestId, (Intent(new ReviseIndependenceRules(revised.ExpectedSequence,
            version.Content)), version.Actor, version));
    }

    Result<T>? Retry<T>(Uuid requestId, object request, ActorReference actor)
    {
        if (!_decisions.TryGetValue(requestId, out var previous))
            return null;
        return previous.Intent == Intent(request) && previous.Actor == actor && previous.Response is T response
            ? Result<T>.Success(response)
            : Failure<T>(RequestErrorKind.Conflict, "The request identity already retains a different rule intent or actor.");
    }

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value,
        value!.GetType(), ComplianceCoreJsonContext.Default).Length <= 48 * 1024;
    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
