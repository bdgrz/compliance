using System.Globalization;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     One program's findings and corrective actions in one stream, with optimistic concurrency
///     per finding. Source wording is immutable; every change is attributable; a linked acceptance
///     never closes a finding or counts as remediation; closure needs completed corrective work,
///     resolution evidence, and an independent decision; reopening keeps the closure history.
/// </summary>
public sealed class RemediationLedger : Aggregate
{
    public const string Open = "open";
    public const string Remediated = "remediated";
    public const string Closed = "closed";
    public const string Accepted = "accepted";
    public const string Overdue = "overdue";
    public const string Unresolved = "unresolved";
    public const string Completed = "completed";
    public const string RiskAcceptance = "risk_acceptance";
    public const string Waiver = "waiver";
    const int MaximumTextLength = 4000;

    static readonly string[] SourceKinds = ["manual", "readiness_gap", "control_occurrence",
        "occurrence_review", "evaluation_deviation", "incident", "consultant_observation",
        "examination_item"];

    static readonly string[] LinkKinds = ["control", "criterion", "evidence", "access_decision",
        "risk", "vendor", "readiness_gap", "control_occurrence"];

    static readonly string[] Severities = ["low", "medium", "high", "critical"];

    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, FindingState> _findings = [];

    public RemediationLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "remediation",
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<FindingRaised>(ev => _findings[ev.FindingId] = new FindingState(ev));
        On<FindingRevised>(ev =>
        {
            var finding = _findings[ev.FindingId];
            finding.Revision = ev.Revision;
            finding.Severity = ev.Severity;
            finding.OwnerMemberId = ev.OwnerMemberId;
            finding.DueOn = ev.DueOn;
            finding.AffectedScope = ev.AffectedScope;
            finding.RootCause = ev.RootCause;
            finding.History.Add(new FindingHistoryEntryView(ev.Revision, "revised",
                $"Severity {ev.Severity}, owner {ev.OwnerMemberId}, due {Date(ev.DueOn)}: {ev.Reason}",
                ev.RevisedBy, ev.RevisedAt));
        });
        On<CorrectiveActionAdded>(ev =>
        {
            var finding = _findings[ev.FindingId];
            finding.Revision = ev.Revision;
            finding.Actions.Add(ev.Action);
            finding.History.Add(new FindingHistoryEntryView(ev.Revision,
                "corrective_action_added", ev.Action.Description, ev.Action.AddedBy,
                ev.Action.AddedAt));
        });
        On<CorrectiveActionCompleted>(ev =>
        {
            var finding = _findings[ev.FindingId];
            finding.Revision = ev.Revision;
            var index = finding.Actions.FindIndex(action => action.ActionId == ev.ActionId);
            finding.Actions[index] = finding.Actions[index] with
            {
                Status = Completed,
                ResolutionNotes = ev.ResolutionNotes,
                Evidence = ev.Evidence,
                CompletedByMemberId = ev.CompletedByMemberId,
                CompletedBy = ev.CompletedBy,
                CompletedAt = ev.CompletedAt,
            };
            finding.History.Add(new FindingHistoryEntryView(ev.Revision,
                "corrective_action_completed", ev.ResolutionNotes, ev.CompletedBy,
                ev.CompletedAt));
        });
        On<FindingAcceptanceLinked>(ev =>
        {
            var finding = _findings[ev.FindingId];
            finding.Revision = ev.Revision;
            finding.Acceptances.Add(ev.Acceptance);
            finding.History.Add(new FindingHistoryEntryView(ev.Revision, "acceptance_linked",
                $"{ev.Acceptance.Kind} {ev.Acceptance.RecordId} until {ev.Acceptance.ExpiresAt:O}",
                ev.Acceptance.LinkedBy, ev.Acceptance.LinkedAt));
        });
        On<FindingClosed>(ev =>
        {
            var finding = _findings[ev.FindingId];
            finding.Revision = ev.Revision;
            finding.Closure = ev.Closure;
            finding.History.Add(new FindingHistoryEntryView(ev.Revision, "closed",
                $"Decision {ev.Closure.DecisionId}: {ev.Closure.Rationale}", ev.Closure.ClosedBy,
                ev.Closure.ClosedAt));
        });
        On<FindingReopened>(ev =>
        {
            var finding = _findings[ev.FindingId];
            finding.Revision = ev.Revision;
            finding.Closure = null;
            finding.History.Add(new FindingHistoryEntryView(ev.Revision, "reopened", ev.Reason,
                ev.ReopenedBy, ev.ReopenedAt));
        });
    }

    public bool Contains(Uuid findingId) => _findings.ContainsKey(findingId);

    public CommandFailure? Raise(Uuid findingId, FindingSource source, string title,
        string description, string severity, string affectedScope, Uuid ownerMemberId,
        DateOnly dueOn, IReadOnlyList<FindingLink> links, ActorReference raisedBy,
        DateTimeOffset raisedAt)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (_findings.TryGetValue(findingId, out var existing))
            return existing.Raised.Source == source &&
                   StringComparer.Ordinal.Equals(existing.Raised.Title, title?.Trim())
                ? null
                : CommandFailure.StateConflict("The finding request was already recorded.");
        if (source is null || !SourceKinds.Contains(source.Kind) ||
            string.IsNullOrWhiteSpace(source.SourceText) || source.SourceText.Length > 8000 ||
            source.Version is { Length: > 200 } ||
            source.Kind is not ("manual" or "incident" or "consultant_observation") &&
            source.RecordId is null)
            return CommandFailure.InvalidContent(
                "A finding source needs a known kind, the source record for a governed source, and source text of at most 8000 characters.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 ||
            !IsBoundedText(description, 8000) || !IsBoundedText(affectedScope, 2000))
            return CommandFailure.InvalidContent(
                "A finding needs a title of at most 200 characters, a description, and an affected scope.");
        if (ValidateTriage(severity, ownerMemberId, dueOn) is { } triage)
            return triage;
        if (links.Count > 50 || links.Any(static link => link is null ||
                !LinkKinds.Contains(link.Kind) || string.IsNullOrWhiteSpace(link.Reference) ||
                link.Reference.Length > 500))
            return CommandFailure.InvalidContent(
                "Each finding link needs a known kind and a reference of at most 500 characters.");
        RaiseEvent(new FindingRaised(_tenantId, Id, findingId, 1, source, title.Trim(),
            description.Trim(), severity, affectedScope.Trim(), ownerMemberId, dueOn,
            links.Select(static link => link with { Reference = link.Reference.Trim() }).ToArray(),
            raisedBy, raisedAt));
        return null;
    }

    public CommandFailure? Revise(Uuid findingId, long expectedRevision, string severity,
        Uuid ownerMemberId, DateOnly dueOn, string affectedScope, string? rootCause,
        string reason, ActorReference actor, DateTimeOffset at)
    {
        if (Writable(findingId, expectedRevision) is { } blocked)
            return blocked;
        if (ValidateTriage(severity, ownerMemberId, dueOn) is { } triage)
            return triage;
        if (!IsBoundedText(affectedScope, 2000) || rootCause is { } cause &&
            cause.Trim().Length > MaximumTextLength || !IsBoundedText(reason, MaximumTextLength))
            return CommandFailure.InvalidContent(
                "A finding revision needs an affected scope, a root cause of at most 4000 characters, and a reason.");
        var finding = _findings[findingId];
        var cleanCause = string.IsNullOrWhiteSpace(rootCause) ? null : rootCause.Trim();
        if (finding.Severity == severity && finding.OwnerMemberId == ownerMemberId &&
            finding.DueOn == dueOn && finding.AffectedScope == affectedScope.Trim() &&
            finding.RootCause == cleanCause)
            return CommandFailure.InvalidContent("The finding revision changes nothing.");
        RaiseEvent(new FindingRevised(_tenantId, Id, findingId, expectedRevision + 1, severity,
            ownerMemberId, dueOn, affectedScope.Trim(), cleanCause, reason.Trim(), actor, at));
        return null;
    }

    public CommandFailure? AddAction(Uuid findingId, long expectedRevision, Uuid actionId,
        string description, Uuid ownerMemberId, DateOnly dueOn, ActorReference actor,
        DateTimeOffset at)
    {
        if (_findings.TryGetValue(findingId, out var existing) &&
            existing.Actions.Exists(action => action.ActionId == actionId))
            return null;
        if (Writable(findingId, expectedRevision) is { } blocked)
            return blocked;
        if (!IsBoundedText(description, MaximumTextLength) || ownerMemberId == Uuid.Empty ||
            dueOn == default)
            return CommandFailure.InvalidContent(
                "A corrective action needs a description of at most 4000 characters, an owner, and a due date.");
        if (_findings[findingId].Actions.Count >= 50)
            return CommandFailure.StateConflict("A finding can have at most 50 corrective actions.");
        RaiseEvent(new CorrectiveActionAdded(_tenantId, Id, findingId, expectedRevision + 1,
            new CorrectiveActionView(actionId, description.Trim(), ownerMemberId, dueOn, Open,
                actor, at)));
        return null;
    }

    public CommandFailure? CompleteAction(Uuid findingId, long expectedRevision, Uuid actionId,
        string resolutionNotes, IReadOnlyList<EvidenceReference> evidence, Uuid completerMemberId,
        ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (Writable(findingId, expectedRevision) is { } blocked)
            return blocked;
        if (_findings[findingId].Actions.Find(action => action.ActionId == actionId) is not
            { } action)
            return CommandFailure.MissingRecord("The corrective action was not found.");
        if (action.Status == Completed)
            return CommandFailure.StateConflict("The corrective action is already completed.");
        if (!IsBoundedText(resolutionNotes, MaximumTextLength) ||
            EvidenceProblem(evidence) is not null)
            return CommandFailure.InvalidContent(
                "Completing a corrective action needs resolution notes and at least one valid evidence reference.");
        RaiseEvent(new CorrectiveActionCompleted(_tenantId, Id, findingId, expectedRevision + 1,
            actionId, resolutionNotes.Trim(), AsUnresolved(evidence), completerMemberId, actor, at));
        return null;
    }

    public CommandFailure? LinkAcceptance(Uuid findingId, long expectedRevision, string kind,
        Uuid recordId, Uuid? decisionId, DateTimeOffset expiresAt, ActorReference actor,
        DateTimeOffset at)
    {
        if (Writable(findingId, expectedRevision) is { } blocked)
            return blocked;
        if (_findings[findingId].Acceptances.Exists(acceptance => acceptance.Kind == kind &&
                acceptance.RecordId == recordId && acceptance.DecisionId == decisionId))
            return CommandFailure.StateConflict("The acceptance is already linked to this finding.");
        if (expiresAt <= at)
            return CommandFailure.StateConflict("An expired acceptance cannot be linked.");
        RaiseEvent(new FindingAcceptanceLinked(_tenantId, Id, findingId, expectedRevision + 1,
            new FindingAcceptanceView(kind, recordId, decisionId, expiresAt, actor, at)));
        return null;
    }

    /// <summary>
    ///     Verifies remediation and closes the finding. Members who own the finding, own or
    ///     completed a corrective action may close only under a waiver scoped to
    ///     (finding, finding_id, finding_id, expected_revision, approve).
    /// </summary>
    public CommandFailure? Close(Uuid findingId, long expectedRevision, Uuid decisionId,
        string verificationRationale, IReadOnlyList<EvidenceReference> resolutionEvidence,
        string rationale, Uuid closerMemberId, ActorReference actor, DateTimeOffset at,
        SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(resolutionEvidence);
        if (Writable(findingId, expectedRevision) is { } blocked)
            return blocked;
        var finding = _findings[findingId];
        if (finding.Actions.Count == 0 ||
            finding.Actions.Exists(static action => action.Status != Completed))
            return CommandFailure.StateConflict(
                "Closing a finding requires completed corrective work; an acceptance is not remediation.");
        if (EvidenceProblem(resolutionEvidence) is not null ||
            !IsBoundedText(verificationRationale, MaximumTextLength) ||
            !IsBoundedText(rationale, MaximumTextLength))
            return CommandFailure.InvalidContent(
                "Closing a finding requires resolution evidence, a verification rationale, and a closure rationale.");
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Finding,
            findingId, findingId, expectedRevision, SeparationOfDutiesActions.Approve);
        if (waiver is not null && (waiver.TenantId != _tenantId ||
                                   !waiver.Allows(scope, closerMemberId, at)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and finding revision.");
        var involved = finding.OwnerMemberId == closerMemberId || finding.Actions.Exists(action =>
            action.OwnerMemberId == closerMemberId || action.CompletedByMemberId == closerMemberId);
        if (involved && waiver is null)
            return CommandFailure.ActorProhibited(
                "A finding or corrective action owner cannot verify and close their own remediation.");
        RaiseEvent(new FindingClosed(_tenantId, Id, findingId, expectedRevision + 1,
            new FindingClosureView(decisionId, verificationRationale.Trim(),
                AsUnresolved(resolutionEvidence), rationale.Trim(), closerMemberId, actor, at,
                waiver?.Id)));
        return null;
    }

    public CommandFailure? Reopen(Uuid findingId, long expectedRevision, string reason,
        ActorReference actor, DateTimeOffset at)
    {
        if (!_findings.TryGetValue(findingId, out var finding))
            return CommandFailure.MissingRecord("The finding was not found.");
        if (expectedRevision != finding.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding",
                finding.Revision));
        if (finding.Closure is null)
            return CommandFailure.StateConflict("Only a closed finding can be reopened.");
        if (!IsBoundedText(reason, MaximumTextLength))
            return CommandFailure.InvalidContent("Reopening a finding requires a reason.");
        RaiseEvent(new FindingReopened(_tenantId, Id, findingId, expectedRevision + 1,
            reason.Trim(), actor, at));
        return null;
    }

    public FindingView? Read(Uuid findingId, DateTimeOffset now) =>
        _findings.TryGetValue(findingId, out var finding) ? ToView(finding, now) : null;

    public IReadOnlyList<FindingView> ReadAll(DateTimeOffset now) => _findings.Values
        .OrderBy(static finding => finding.Raised.RaisedAt)
        .ThenBy(static finding => finding.Raised.FindingId.ToString(), StringComparer.Ordinal)
        .Select(finding => ToView(finding, now)).ToArray();

    public static string Materiality(string severity) => severity switch
    {
        "critical" or "high" => "high",
        "medium" => "medium",
        _ => "low",
    };

    FindingView ToView(FindingState finding, DateTimeOffset now)
    {
        var raised = finding.Raised;
        var acceptances = finding.Acceptances.Select(acceptance => acceptance with
        {
            State = acceptance.ExpiresAt > now ? "active" : "expired",
        }).ToArray();
        var remediated = finding.Actions.Count > 0 &&
                         finding.Actions.TrueForAll(static action => action.Status == Completed);
        var status = finding.Closure is not null ? Closed : remediated ? Remediated : Open;
        var readiness = finding.Closure is not null ? Closed
            : remediated ? Remediated
            : acceptances.Any(static acceptance => acceptance.State == "active") ? Accepted
            : finding.DueOn < DateOnly.FromDateTime(now.UtcDateTime) ? Overdue
            : Unresolved;
        return new FindingView(_tenantId, Id, raised.FindingId, finding.Revision, raised.Source,
            raised.Title, raised.Description, finding.Severity, finding.AffectedScope,
            finding.OwnerMemberId, finding.DueOn, finding.RootCause, status, readiness,
            raised.Links, finding.Actions.ToArray(), acceptances, finding.Closure,
            finding.History.ToArray(), raised.RaisedBy, raised.RaisedAt);
    }

    CommandFailure? Writable(Uuid findingId, long expectedRevision)
    {
        if (!_findings.TryGetValue(findingId, out var finding))
            return CommandFailure.MissingRecord("The finding was not found.");
        if (expectedRevision != finding.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding",
                finding.Revision));
        return finding.Closure is not null
            ? CommandFailure.StateConflict("The finding is closed. Reopen it to change it.")
            : null;
    }

    static CommandFailure? ValidateTriage(string severity, Uuid ownerMemberId, DateOnly dueOn) =>
        !Severities.Contains(severity) || ownerMemberId == Uuid.Empty || dueOn == default
            ? CommandFailure.InvalidContent(
                "A finding needs a severity of low, medium, high, or critical, an owner, and a due date.")
            : null;

    static string? EvidenceProblem(IReadOnlyList<EvidenceReference> evidence) =>
        evidence.Count is < 1 or > 50 || evidence.Any(static item => item is null ||
            item.Kind is not ("artifact" or "record" or "external") ||
            string.IsNullOrWhiteSpace(item.Reference) || item.Reference.Length > 2000 ||
            item.Description is { Length: > 2000 })
            ? "invalid"
            : null;

    static EvidenceReference[] AsUnresolved(IReadOnlyList<EvidenceReference> evidence) =>
        evidence.Select(static item => new EvidenceReference(item.ExpectedEvidenceIndex, item.Kind,
            item.Reference.Trim(), string.IsNullOrWhiteSpace(item.Description)
                ? null
                : item.Description.Trim(), "unresolved")).ToArray();

    static bool IsBoundedText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;

    static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    sealed class FindingState(FindingRaised raised)
    {
        public FindingRaised Raised { get; } = raised;
        public long Revision { get; set; } = raised.Revision;
        public string Severity { get; set; } = raised.Severity;
        public Uuid OwnerMemberId { get; set; } = raised.OwnerMemberId;
        public DateOnly DueOn { get; set; } = raised.DueOn;
        public string AffectedScope { get; set; } = raised.AffectedScope;
        public string? RootCause { get; set; }
        public FindingClosureView? Closure { get; set; }
        public List<CorrectiveActionView> Actions { get; } = [];
        public List<FindingAcceptanceView> Acceptances { get; } = [];
        public List<FindingHistoryEntryView> History { get; } =
        [
            new(raised.Revision, "raised", raised.Title, raised.RaisedBy, raised.RaisedAt),
        ];
    }
}
