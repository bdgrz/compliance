using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A policy acknowledgement or training campaign (M0-D12): exact material, a frozen launch
///     audience from one roster snapshot, attributed joiner/mover/leaver amendments, per-person
///     acknowledgements or completions, bounded exceptions, and a reconcilable result.
/// </summary>
public sealed class PolicyDistributionCampaign : Aggregate
{
    public const string DefaultAcknowledgementText =
        "I have read, understood, and agree to comply with this exact policy version.";
    public const int BatchSize = 80;
    public const int MaximumAudience = 10000;
    const int JoinerDays = 30;
    readonly Uuid _tenantId;
    bool _launched;
    bool _closed;
    long _reconciliations;
    readonly Dictionary<Uuid, CampaignParticipant> _participants = [];
    readonly List<CampaignParticipant> _order = [];
    readonly List<CampaignAmendmentView> _amendments = [];
    readonly HashSet<Uuid> _reconciledSnapshots = [];
    PolicyCampaignLaunched? _launch;
    PolicyCampaignClosed? _closing;

    public bool IsLaunched => _launched;
    public Uuid ProgramId => _launch?.ProgramId ?? Uuid.Empty;
    public CampaignSubject? Subject => _launch?.Subject;
    public Uuid LatestRosterSnapshotId { get; private set; }
    public long Reconciliations => _reconciliations;
    public string AudienceKind => _launch?.AudienceKind ?? string.Empty;
    public IReadOnlyList<string> AudienceTeams => _launch?.AudienceTeams ?? [];

    public string? DisplayNameOf(Uuid personId) =>
        _participants.GetValueOrDefault(personId)?.DisplayName;

    public PolicyDistributionCampaign(Uuid tenantId, Uuid campaignId)
        : base(campaignId, new EventStreamAddress(tenantId.ToString(),
            "policy-distribution-campaigns", campaignId.ToString()))
    {
        _tenantId = tenantId;
        On<PolicyCampaignLaunched>(ev =>
        {
            _launched = true;
            _launch = ev;
            LatestRosterSnapshotId = ev.RosterSnapshotId;
            _reconciledSnapshots.Add(ev.RosterSnapshotId);
        });
        On<PolicyCampaignAudienceFrozen>(ev =>
        {
            foreach (var entry in ev.Entries)
                Add(new CampaignParticipant(entry.PersonId, entry.DisplayName, entry.WorkerType,
                    entry.Department, true, "launch", _launch!.RosterSnapshotId, entry.DueOn));
        });
        On<PolicyCampaignAudienceAmended>(ev =>
        {
            _reconciliations = ev.Reconciliation;
            LatestRosterSnapshotId = ev.RosterSnapshotId;
            _reconciledSnapshots.Add(ev.RosterSnapshotId);
            foreach (var amendment in ev.Amendments)
            {
                _amendments.Add(new CampaignAmendmentView(ev.Reconciliation, amendment.PersonId,
                    amendment.DisplayName, amendment.Reason, amendment.DueOn,
                    ev.RosterSnapshotId, ev.RosterContentSha256, ev.Actor, ev.AmendedAt));
                if (amendment.Reason is "joiner" or "mover_in")
                {
                    if (_participants.TryGetValue(amendment.PersonId, out var returning))
                    {
                        returning.RemovedReason = null;
                        returning.RemovedBy = null;
                        returning.Inclusion = amendment.Reason;
                        returning.IncludedBy = ev.RosterSnapshotId;
                        returning.DueOn = amendment.DueOn!.Value;
                        returning.Department = amendment.Department;
                        returning.WorkerType = amendment.WorkerType!;
                    }
                    else
                        Add(new CampaignParticipant(amendment.PersonId, amendment.DisplayName,
                            amendment.WorkerType!, amendment.Department, false, amendment.Reason,
                            ev.RosterSnapshotId, amendment.DueOn!.Value));
                }
                else if (_participants.TryGetValue(amendment.PersonId, out var leaving))
                {
                    leaving.RemovedReason = amendment.Reason;
                    leaving.RemovedBy = ev.RosterSnapshotId;
                }
            }
        });
        On<PolicyAcknowledgementRecorded>(ev =>
            _participants[ev.PersonId].Acknowledgement = new CampaignAcknowledgementView(
                ev.AcknowledgementId, ev.PersonId, ev.PolicyVersion, ev.ContentSha256,
                ev.AcknowledgementText, ev.Performer, ev.Recorder, ev.RecordedOnBehalf,
                ev.AcknowledgedAt));
        On<TrainingCompletionRecorded>(ev =>
            _participants[ev.PersonId].Completion = new CampaignCompletionView(ev.CompletionId,
                ev.PersonId, ev.RequirementVersion, ev.CompletedOn, ev.Source,
                ev.EvidenceReference, ev.Recorder, ev.RecordedAt));
        On<CampaignWaiverApproved>(ev =>
            _participants[ev.PersonId].Waiver = new CampaignWaiverView(ev.WaiverId,
                ev.PersonId, ev.Reason, ev.ExpiresOn, ev.Approver, ev.ApprovedAt));
        On<PolicyCampaignClosed>(ev =>
        {
            _closed = true;
            _closing = ev;
        });
    }

    void Add(CampaignParticipant participant)
    {
        _participants.Add(participant.PersonId, participant);
        _order.Add(participant);
    }

    public bool HasReconciled(Uuid snapshotId) => _reconciledSnapshots.Contains(snapshotId);

    public Result<CampaignRegistration> Launch(Uuid programId, CampaignSubject subject,
        string audienceKind, IReadOnlyList<string> audienceTeams, Uuid rosterSnapshotId,
        string rosterContentSha256, RosterAudience audience, DateOnly dueOn,
        string? instructions, ActorReference actor, Uuid actorMemberId, DateTimeOffset launchedAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(audience);
        if (_launched)
            return _launch!.ProgramId == programId && _launch.Subject == subject &&
                   _launch.RosterSnapshotId == rosterSnapshotId && _launch.DueOn == dueOn
                ? Result<CampaignRegistration>.Success(new CampaignRegistration(Id,
                    _launch.AudienceCount))
                : Result<CampaignRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The campaign was already launched with different terms."));
        var launchedOn = DateOnly.FromDateTime(launchedAt.UtcDateTime);
        if (dueOn < launchedOn)
            return Invalid("A campaign due date cannot be before its launch.");
        if (instructions?.Length > 4000)
            return Invalid("Campaign instructions must be at most 4000 characters.");
        if (audience.Matching.Count > MaximumAudience)
            return Invalid($"A campaign audience is limited to {MaximumAudience} people.");
        var entries = audience.Matching.Values
            .OrderBy(static match => match.PersonId.ToString(), StringComparer.Ordinal)
            .Select(match => new CampaignAudienceEntry(match.PersonId, match.DisplayName,
                match.WorkerType, match.Department, match.StartDate,
                Max(dueOn, match.StartDate.AddDays(JoinerDays))))
            .ToArray();
        RaiseEvent(new PolicyCampaignLaunched(_tenantId, programId, Id, subject, audienceKind,
            audienceTeams, rosterSnapshotId, rosterContentSha256, launchedOn, dueOn,
            string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim(),
            subject.Kind == "policy" ? DefaultAcknowledgementText : null, entries.Length, actor,
            actorMemberId, launchedAt));
        var batch = 0;
        foreach (var chunk in entries.Chunk(BatchSize))
            RaiseEvent(new PolicyCampaignAudienceFrozen(_tenantId, Id, ++batch, chunk));
        return Result<CampaignRegistration>.Success(new CampaignRegistration(Id, entries.Length));
    }

    /// <summary>
    ///     Records attributed amendments from a later roster snapshot. Removed people keep their
    ///     history; the frozen launch audience is never edited.
    /// </summary>
    public Result<CampaignReconciliationView> Reconcile(Uuid programId, Uuid rosterSnapshotId,
        string rosterContentSha256, RosterAudience current, IReadOnlySet<Uuid> previouslyPresent,
        ActorReference actor, DateTimeOffset amendedAt)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previouslyPresent);
        if (CheckOpen(programId) is { } failure)
            return Result<CampaignReconciliationView>.Failure(failure.ToRequestError());
        if (_reconciledSnapshots.Contains(rosterSnapshotId))
            return Result<CampaignReconciliationView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The campaign already reconciled that roster snapshot."));
        var observedOn = DateOnly.FromDateTime(amendedAt.UtcDateTime);
        var amendments = new List<CampaignAudienceAmendment>();
        foreach (var participant in _order.Where(static participant => participant.IsActive))
            if (!current.Matching.ContainsKey(participant.PersonId))
                amendments.Add(new CampaignAudienceAmendment(participant.PersonId,
                    participant.DisplayName,
                    current.Present.Contains(participant.PersonId) ? "mover_out" : "leaver",
                    null, null, null));
        foreach (var match in current.Matching.Values.OrderBy(static match =>
                     match.PersonId.ToString(), StringComparer.Ordinal))
        {
            if (_participants.TryGetValue(match.PersonId, out var existing) && existing.IsActive)
                continue;
            var joiner = !previouslyPresent.Contains(match.PersonId);
            amendments.Add(new CampaignAudienceAmendment(match.PersonId, match.DisplayName,
                joiner ? "joiner" : "mover_in", match.WorkerType, match.Department,
                joiner
                    ? match.StartDate.AddDays(JoinerDays)
                    : Max(_launch!.DueOn, observedOn.AddDays(JoinerDays))));
        }
        var reconciliation = _reconciliations + 1;
        var batch = 0;
        var chunks = amendments.Count == 0
            ? [[]]
            : amendments.Chunk(BatchSize).ToArray();
        foreach (var chunk in chunks)
            RaiseEvent(new PolicyCampaignAudienceAmended(_tenantId, Id, reconciliation, ++batch,
                rosterSnapshotId, rosterContentSha256, observedOn, chunk, actor, amendedAt));
        return Result<CampaignReconciliationView>.Success(new CampaignReconciliationView(Id,
            reconciliation, rosterSnapshotId, amendments));
    }

    /// <summary>Records the exact acknowledgement; a later version never satisfies this campaign.</summary>
    public CommandFailure? Acknowledge(Uuid programId, Uuid acknowledgementId, Uuid personId,
        long policyVersion, string contentSha256, string acknowledgementText,
        ActorReference performer, ActorReference recorder, bool recordedOnBehalf,
        DateTimeOffset acknowledgedAt)
    {
        if (CheckOpen(programId) is { } failure)
            return failure;
        if (_launch!.Subject.Kind != "policy")
            return CommandFailure.StateConflict("A training campaign records completions, not acknowledgements.");
        if (FindActive(personId) is not { } participant)
            return CommandFailure.MissingRecord("The person is not in this campaign's current audience.");
        if (policyVersion != _launch.Subject.Version ||
            !StringComparer.Ordinal.Equals(contentSha256, _launch.Subject.ContentSha256))
            return CommandFailure.StateConflict(
                "The acknowledgement must name this campaign's exact policy version and content hash.");
        if (!StringComparer.Ordinal.Equals(acknowledgementText, _launch.AcknowledgementText))
            return CommandFailure.InvalidContent(
                "The acknowledgement must store the exact acknowledgement text shown.");
        if (participant.Acknowledgement is { } existing)
            return existing.AcknowledgementId == acknowledgementId
                ? null
                : CommandFailure.StateConflict("The person already acknowledged this campaign.");
        RaiseEvent(new PolicyAcknowledgementRecorded(_tenantId, Id, acknowledgementId, personId,
            _launch.Subject.RecordId, policyVersion, contentSha256, acknowledgementText, performer,
            recorder, recordedOnBehalf, acknowledgedAt));
        return null;
    }

    public CommandFailure? RecordCompletion(Uuid programId, Uuid completionId, Uuid personId,
        long requirementVersion, DateOnly completedOn, string source, string evidenceReference,
        ActorReference recorder, DateTimeOffset recordedAt)
    {
        if (CheckOpen(programId) is { } failure)
            return failure;
        if (_launch!.Subject.Kind != "training")
            return CommandFailure.StateConflict("A policy campaign records acknowledgements, not completions.");
        if (FindActive(personId) is not { } participant)
            return CommandFailure.MissingRecord("The person is not in this campaign's current audience.");
        if (requirementVersion != _launch.Subject.Version)
            return CommandFailure.StateConflict(
                "The completion must name this campaign's exact training requirement version.");
        if (source is not ("manual" or "lms_export") ||
            string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference.Length > 1024)
            return CommandFailure.InvalidContent(
                "A completion requires a manual or lms_export source and an evidence reference of at most 1024 characters.");
        if (completedOn > DateOnly.FromDateTime(recordedAt.UtcDateTime))
            return CommandFailure.InvalidContent("A completion cannot be dated in the future.");
        if (participant.Completion is { } existing)
            return existing.CompletionId == completionId
                ? null
                : CommandFailure.StateConflict("A completion is already recorded for this person.");
        RaiseEvent(new TrainingCompletionRecorded(_tenantId, Id, completionId, personId,
            _launch.Subject.RecordId, requirementVersion, completedOn, source,
            evidenceReference.Trim(), recorder, recordedAt));
        return null;
    }

    /// <summary>Approves an exception of at most 12 months; it never counts as acknowledgement.</summary>
    public CommandFailure? ApproveWaiver(Uuid programId, Uuid exceptionId, Uuid personId,
        string reason, DateOnly expiresOn, ActorReference approver, Uuid approverMemberId,
        DateTimeOffset approvedAt)
    {
        if (CheckOpen(programId) is { } failure)
            return failure;
        if (FindActive(personId) is not { } participant)
            return CommandFailure.MissingRecord("The person is not in this campaign's current audience.");
        var today = DateOnly.FromDateTime(approvedAt.UtcDateTime);
        if (participant.Waiver is { } existing && existing.ExpiresOn > today)
            return existing.WaiverId == exceptionId
                ? null
                : CommandFailure.StateConflict("The person already has an active exception.");
        if (participant.Acknowledgement is not null || participant.Completion is not null)
            return CommandFailure.StateConflict("A satisfied person needs no exception.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 4000)
            return CommandFailure.InvalidContent("An exception requires a reason of at most 4000 characters.");
        if (expiresOn <= today || expiresOn > today.AddMonths(12))
            return CommandFailure.InvalidContent("An exception must expire within 12 months.");
        RaiseEvent(new CampaignWaiverApproved(_tenantId, Id, exceptionId, personId,
            reason.Trim(), expiresOn, approver, approverMemberId, approvedAt));
        return null;
    }

    public CommandFailure? Close(Uuid programId, string rationale, ActorReference actor,
        DateTimeOffset closedAt)
    {
        if (CheckOpen(programId) is { } failure)
            return failure;
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000)
            return CommandFailure.InvalidContent("Closing a campaign requires a rationale of at most 4000 characters.");
        var closedOn = DateOnly.FromDateTime(closedAt.UtcDateTime);
        RaiseEvent(new PolicyCampaignClosed(_tenantId, Id, closedOn, rationale.Trim(),
            Totals(closedOn), actor, closedAt));
        return null;
    }

    CommandFailure? CheckOpen(Uuid programId)
    {
        if (!_launched || _launch!.ProgramId != programId)
            return CommandFailure.MissingRecord("The campaign was not found.");
        return _closed ? CommandFailure.StateConflict("The campaign is closed.") : null;
    }

    CampaignParticipant? FindActive(Uuid personId) =>
        _participants.TryGetValue(personId, out var participant) && participant.IsActive
            ? participant
            : null;

    public bool Contains(Uuid personId) => FindActive(personId) is not null;

    public CampaignAcknowledgementView? FindAcknowledgement(Uuid personId) =>
        _participants.GetValueOrDefault(personId)?.Acknowledgement;

    public CampaignCompletionView? FindCompletion(Uuid personId) =>
        _participants.GetValueOrDefault(personId)?.Completion;

    public CampaignWaiverView? FindWaiver(Uuid personId) =>
        _participants.GetValueOrDefault(personId)?.Waiver;

    /// <summary>The person's state as of a date; results recorded later do not count.</summary>
    public string StateOf(Uuid personId, DateOnly asOf) => State(_participants[personId], asOf);

    static string State(CampaignParticipant participant, DateOnly asOf)
    {
        if (!participant.IsActive)
            return "removed";
        if (participant.Acknowledgement is { } acknowledgement &&
            DateOnly.FromDateTime(acknowledgement.AcknowledgedAt.UtcDateTime) <= asOf)
            return "acknowledged";
        if (participant.Completion is { } completion && completion.CompletedOn <= asOf &&
            DateOnly.FromDateTime(completion.RecordedAt.UtcDateTime) <= asOf)
            return "completed";
        if (participant.Waiver is { } exception &&
            DateOnly.FromDateTime(exception.ApprovedAt.UtcDateTime) <= asOf &&
            asOf < exception.ExpiresOn)
            return "excepted";
        return asOf > participant.DueOn ? "overdue" : "pending";
    }

    public CampaignTotalsView Totals(DateOnly asOf)
    {
        var states = _order.Select(participant => State(participant, asOf)).ToArray();
        var launch = _order.Count(static participant => participant.LaunchMember);
        return new CampaignTotalsView(_order.Count, launch, _order.Count - launch,
            states.Count(static state => state == "removed"),
            states.Count(static state => state == "pending"),
            states.Count(static state => state == "overdue"),
            states.Count(static state => state is "acknowledged" or "completed"),
            states.Count(static state => state == "excepted"));
    }

    public CampaignView ToView(DateOnly asOf)
    {
        var launch = _launch ?? throw new InvalidOperationException("The campaign was not launched.");
        return new CampaignView(_tenantId, launch.ProgramId, Id, launch.Subject,
            launch.AudienceKind, launch.AudienceTeams, launch.RosterSnapshotId,
            launch.RosterContentSha256, LatestRosterSnapshotId, launch.LaunchedOn, launch.DueOn,
            launch.Instructions, launch.AcknowledgementText, _closed ? "closed" : "open", asOf,
            Totals(asOf), launch.Actor, launch.LaunchedAt, _closing?.Actor, _closing?.ClosedAt,
            _closing?.Totals);
    }

    public IReadOnlyList<CampaignParticipantView> Participants(DateOnly asOf, string? state) =>
        _order.Select(participant => new CampaignParticipantView(participant.PersonId,
                participant.DisplayName, participant.WorkerType, participant.Department,
                participant.Inclusion, participant.IncludedBy, State(participant, asOf),
                participant.DueOn, participant.RemovedReason, participant.RemovedBy,
                participant.Acknowledgement, participant.Completion, participant.Waiver))
            .Where(view => state is null || view.State == state)
            .ToArray();

    public IReadOnlyList<CampaignAmendmentView> Amendments() => _amendments.ToArray();

    static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;

    static Result<CampaignRegistration> Invalid(string message) =>
        Result<CampaignRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
            message));
}
