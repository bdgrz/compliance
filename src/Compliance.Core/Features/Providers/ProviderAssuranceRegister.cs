using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Owns a tenant's assurance reports, personal due-diligence reviews and immutable logical
///     request decisions. Provider existence and materiality are observed by the handler from the
///     provider register and retained as historical facts; no cross-stream fence is claimed.
/// </summary>
public sealed class ProviderAssuranceRegister : Aggregate
{
    public const string Area = "provider-assurance";
    public const int MaximumPayloadBytes = 48 * 1024;
    public const int MaximumRecordsPerProvider = 200;
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, AssuranceReportView> _reports = [];
    readonly Dictionary<Uuid, ProviderReviewView> _reviews = [];
    readonly Dictionary<Uuid, Decision> _decisions = [];

    public ProviderAssuranceRegister(Uuid tenantId)
        : base(tenantId, new EventStreamAddress(tenantId.ToString(), Area, tenantId.ToString()))
    {
        _tenantId = tenantId;
        On<AssuranceReportRecorded>(ev => ApplyReport(ev.TenantId, ev.ReportId, ev.ProviderId, ev.RequestId, 1,
            ev.Content, ev.ProviderRevision, ev.Actor, ev.RecordedAt));
        On<AssuranceReportRevised>(ev => ApplyReport(ev.TenantId, ev.ReportId, ev.ProviderId, ev.RequestId, ev.Revision,
            ev.Content, ev.ProviderRevision, ev.Actor, ev.RecordedAt));
        On<ProviderReviewRecorded>(ApplyReview);
    }

    public AssuranceReportView? Report(Uuid reportId) => _reports.GetValueOrDefault(reportId);

    public IReadOnlyList<AssuranceReportView> Reports(Uuid providerId) =>
        _reports.Values.Where(report => report.ProviderId == providerId).ToArray();

    public IReadOnlyList<ProviderReviewView> Reviews(Uuid providerId) =>
        _reviews.Values.Where(review => review.ProviderId == providerId).ToArray();

    /// <summary>Checks retained authored input before a handler resolves mutable external sources.</summary>
    public Result<AssuranceReportRegistration>? CheckReportRetry(Uuid reportId, Uuid providerId, Uuid requestId,
        long? expectedRevision, AssuranceReportContent content)
    {
        if (AssuranceRules.InputError(content) is { } inputError)
            return ReportFailure(RequestErrorKind.Validation, inputError);
        if (!_decisions.TryGetValue(requestId, out var decision))
            return null;
        var intent = JsonSerializer.SerializeToUtf8Bytes(AssuranceRules.Normalize(content),
            ComplianceCoreJsonContext.Default.AssuranceReportContent);
        return decision.Kind == "report" && decision.EntityId == reportId && decision.ProviderId == providerId &&
               decision.ExpectedRevision == expectedRevision && decision.Intent.AsSpan().SequenceEqual(intent)
            ? Result<AssuranceReportRegistration>.Success(new AssuranceReportRegistration(reportId, decision.Revision))
            : ReportFailure(RequestErrorKind.Conflict, "The request already has a different retained assurance decision.");
    }

    public Result<ProviderReviewRegistration>? CheckReviewRetry(Uuid reviewId, Uuid providerId, Uuid requestId,
        ProviderReviewContent content)
    {
        if (AssuranceRules.InputError(content) is { } inputError)
            return ReviewFailure(RequestErrorKind.Validation, inputError);
        if (!_decisions.TryGetValue(requestId, out var decision))
            return null;
        var intent = JsonSerializer.SerializeToUtf8Bytes(AssuranceRules.Normalize(content),
            ComplianceCoreJsonContext.Default.ProviderReviewContent);
        return decision.Kind == "review" && decision.EntityId == reviewId && decision.ProviderId == providerId &&
               decision.Intent.AsSpan().SequenceEqual(intent)
            ? Result<ProviderReviewRegistration>.Success(new ProviderReviewRegistration(reviewId))
            : ReviewFailure(RequestErrorKind.Conflict, "The request already has a different retained assurance decision.");
    }

    public Result<AssuranceReportRegistration> RecordReport(Uuid reportId, Uuid providerId, Uuid requestId,
        AssuranceReportContent content, long providerRevision, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (AssuranceRules.InputError(content) is { } inputError)
            return ReportFailure(RequestErrorKind.Validation, inputError);
        content = AssuranceRules.Normalize(content);
        if (CheckReportRetry(reportId, providerId, requestId, null, content) is { } retry)
            return retry;
        if (ValidateReport(reportId, providerId, requestId, content) is { } error)
            return Result<AssuranceReportRegistration>.Failure(error);
        if (_reports.ContainsKey(reportId))
            return ReportFailure(RequestErrorKind.Conflict, "The assurance report identity already exists.");
        if (_reports.Values.Count(report => report.ProviderId == providerId) >= MaximumRecordsPerProvider)
            return ReportFailure(RequestErrorKind.Validation, "The provider has reached its bound of retained assurance reports.");
        var ev = new AssuranceReportRecorded(_tenantId, reportId, providerId, requestId, content, providerRevision, actor, recordedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.AssuranceReportRecorded).Length > MaximumPayloadBytes)
            return ReportFailure(RequestErrorKind.Validation, "The assurance report exceeds the bounded event payload.");
        RaiseEvent(ev);
        return Result<AssuranceReportRegistration>.Success(new AssuranceReportRegistration(reportId, 1));
    }

    public Result<AssuranceReportRegistration> ReviseReport(Uuid reportId, Uuid providerId, Uuid requestId,
        long expectedRevision, AssuranceReportContent content, long providerRevision, ActorReference actor,
        DateTimeOffset recordedAt)
    {
        if (AssuranceRules.InputError(content) is { } inputError)
            return ReportFailure(RequestErrorKind.Validation, inputError);
        content = AssuranceRules.Normalize(content);
        if (CheckReportRetry(reportId, providerId, requestId, expectedRevision, content) is { } retry)
            return retry;
        if (!_reports.TryGetValue(reportId, out var current) || current.ProviderId != providerId)
            return ReportFailure(RequestErrorKind.NotFound, "The assurance report was not found.");
        if (expectedRevision != current.Revision)
            return Result<AssuranceReportRegistration>.Failure(
                VersionedRecordRules.StaleRevision("assurance report", current.Revision).ToRequestError());
        if (ValidateReport(reportId, providerId, requestId, content) is { } error)
            return Result<AssuranceReportRegistration>.Failure(error);
        var ev = new AssuranceReportRevised(_tenantId, reportId, providerId, requestId, current.Revision + 1, content,
            providerRevision, actor, recordedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.AssuranceReportRevised).Length > MaximumPayloadBytes)
            return ReportFailure(RequestErrorKind.Validation, "The assurance report exceeds the bounded event payload.");
        RaiseEvent(ev);
        return Result<AssuranceReportRegistration>.Success(new AssuranceReportRegistration(reportId, ev.Revision));
    }

    /// <summary>
    ///     Appends an immutable review. <paramref name="providerMateriality" /> is the observed
    ///     classification: only an explicit <c>not_material</c> lifts the one-year cadence bound.
    /// </summary>
    public Result<ProviderReviewRegistration> RecordReview(Uuid reviewId, Uuid providerId, Uuid requestId,
        ProviderReviewContent content, long providerRevision, string? providerMateriality, ActorReference actor,
        DateTimeOffset recordedAt)
    {
        if (AssuranceRules.InputError(content) is { } inputError)
            return ReviewFailure(RequestErrorKind.Validation, inputError);
        content = AssuranceRules.Normalize(content);
        if (CheckReviewRetry(reviewId, providerId, requestId, content) is { } retry)
            return retry;
        if (reviewId == Uuid.Empty || providerId == Uuid.Empty || requestId == Uuid.Empty)
            return ReviewFailure(RequestErrorKind.Validation, "A provider review requires nonempty identities.");
        if (AssuranceRules.Validate(content) is { } error)
            return ReviewFailure(RequestErrorKind.Validation, error);
        if (content.ReviewedAt > DateOnly.FromDateTime(recordedAt.UtcDateTime))
            return ReviewFailure(RequestErrorKind.Validation, "A review cannot be dated after it is recorded.");
        if (providerMateriality != "not_material" &&
            content.NextReviewDue > content.ReviewedAt.AddMonths(AssuranceRules.AnnualMonths))
            return ReviewFailure(RequestErrorKind.Validation,
                "A material or unclassified provider is reviewed at least annually (M0-D11).");
        long? reportRevision = null;
        if (content.AssuranceReportId is { } reportId)
        {
            if (!_reports.TryGetValue(reportId, out var report) || report.ProviderId != providerId)
                return ReviewFailure(RequestErrorKind.NotFound, "The assurance report was not found for this provider.");
            if (report.Content.ReportKind != content.EvidenceKind)
                return ReviewFailure(RequestErrorKind.Validation, "The evidence kind must match the assurance report kind.");
            if (content.ReviewedAt < report.Content.PeriodEnd)
                return ReviewFailure(RequestErrorKind.Validation, "A review cannot precede the end of its report period.");
            if (content.Conclusion != "not_acceptable" &&
                ProviderAssuranceCoverage.Currency(report.Content, content.ReviewedAt) == "stale")
                return ReviewFailure(RequestErrorKind.Validation,
                    "A stale report cannot support an acceptable conclusion; record the review as not_acceptable.");
            reportRevision = report.Revision;
        }
        if (_reviews.Values.Count(review => review.ProviderId == providerId) >= MaximumRecordsPerProvider)
            return ReviewFailure(RequestErrorKind.Validation, "The provider has reached its bound of retained reviews.");
        if (_reviews.ContainsKey(reviewId))
            return ReviewFailure(RequestErrorKind.Conflict, "The provider review identity already exists.");
        var ev = new ProviderReviewRecorded(_tenantId, reviewId, providerId, requestId, content, providerRevision,
            providerMateriality, reportRevision, actor, recordedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev, ComplianceCoreJsonContext.Default.ProviderReviewRecorded).Length > MaximumPayloadBytes)
            return ReviewFailure(RequestErrorKind.Validation, "The provider review exceeds the bounded event payload.");
        RaiseEvent(ev);
        return Result<ProviderReviewRegistration>.Success(new ProviderReviewRegistration(reviewId));
    }

    static RequestError? ValidateReport(Uuid reportId, Uuid providerId, Uuid requestId, AssuranceReportContent content)
    {
        if (reportId == Uuid.Empty || providerId == Uuid.Empty || requestId == Uuid.Empty)
            return new RequestError(RequestErrorKind.Validation, "An assurance report requires nonempty identities.");
        return AssuranceRules.Validate(content) is { } error ? new RequestError(RequestErrorKind.Validation, error) : null;
    }

    void ApplyReport(Uuid tenantId, Uuid reportId, Uuid providerId, Uuid requestId, long revision,
        AssuranceReportContent content, long providerRevision, ActorReference actor, DateTimeOffset recordedAt)
    {
        var previous = _reports.GetValueOrDefault(reportId);
        if (tenantId != _tenantId || revision != (previous?.Revision ?? 0) + 1 ||
            previous is not null && previous.ProviderId != providerId)
            throw new InvalidOperationException("An assurance report must follow its tenant, provider and previous revision.");
        content = AssuranceRules.Normalize(content);
        _reports[reportId] = new AssuranceReportView(_tenantId, reportId, providerId, revision, content, providerRevision,
            content.Exceptions!.Count, content.ComplementaryControls!.Count, content.CoverageGaps!.Count, actor, recordedAt);
        _decisions.Add(requestId, new Decision("report", reportId, providerId, revision == 1 ? null : revision - 1,
            JsonSerializer.SerializeToUtf8Bytes(content, ComplianceCoreJsonContext.Default.AssuranceReportContent), revision));
    }

    void ApplyReview(ProviderReviewRecorded ev)
    {
        if (ev.TenantId != _tenantId || _reviews.ContainsKey(ev.ReviewId))
            throw new InvalidOperationException("A provider review must be a new review of its tenant.");
        var content = AssuranceRules.Normalize(ev.Content);
        _reviews[ev.ReviewId] = new ProviderReviewView(_tenantId, ev.ReviewId, ev.ProviderId, content, ev.ProviderRevision,
            ev.AssuranceReportRevision, ev.Actor, ev.RecordedAt);
        _decisions.Add(ev.RequestId, new Decision("review", ev.ReviewId, ev.ProviderId, null,
            JsonSerializer.SerializeToUtf8Bytes(content, ComplianceCoreJsonContext.Default.ProviderReviewContent), 1));
    }

    static Result<AssuranceReportRegistration> ReportFailure(RequestErrorKind kind, string message) =>
        Result<AssuranceReportRegistration>.Failure(new RequestError(kind, message));

    static Result<ProviderReviewRegistration> ReviewFailure(RequestErrorKind kind, string message) =>
        Result<ProviderReviewRegistration>.Failure(new RequestError(kind, message));

    sealed record Decision(string Kind, Uuid EntityId, Uuid ProviderId, long? ExpectedRevision, byte[] Intent, long Revision);
}
