using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Holds every criterion-level not-applicable decision of one program in one stream. A
///     decision is keyed by (edition_id, criterion identifier); each proposal is a version that
///     counts only after an independent accepted review, and a withdrawal restores coverage
///     while keeping the full history.
/// </summary>
public sealed class CriterionApplicabilityLedger : Aggregate
{
    public const int MaximumTextLength = 4000;
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, CriterionApplicabilityView> _decisions = [];
    readonly Dictionary<(Uuid DecisionId, int Version), Uuid> _proposers = [];
    readonly List<Uuid> _order = [];

    public CriterionApplicabilityLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), "criterion-applicability",
            programId.ToString()))
    {
        _tenantId = tenantId;
        On<CriterionNotApplicableProposed>(ev =>
        {
            if (!_decisions.ContainsKey(ev.DecisionId))
                _order.Add(ev.DecisionId);
            _proposers[(ev.DecisionId, ev.VersionNumber)] = ev.ProposerMemberId;
            Fold(ev.DecisionId, ev);
        });
        On<CriterionApplicabilityReviewed>(ev => Fold(ev.DecisionId, ev));
        On<CriterionNotApplicableWithdrawn>(ev => Fold(ev.DecisionId, ev));
    }

    public static Uuid DecisionIdFor(Uuid programId, Uuid editionId, string criterionIdentifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(programId, "criterion-applicability"),
            editionId + "|" + criterionIdentifier);

    public CriterionApplicabilityView? Read(Uuid decisionId) =>
        _decisions.GetValueOrDefault(decisionId);

    public IReadOnlyList<CriterionApplicabilityView> ReadAll() =>
        [.. _order.Select(id => _decisions[id])];

    public CommandFailure? Propose(Uuid editionId, string criterionIdentifier,
        long expectedRevision, string rationale, Uuid actorMemberId, ActorReference actor,
        DateTimeOffset proposedAt, out CriterionApplicabilityRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(actor);
        registration = null;
        if (editionId == Uuid.Empty || string.IsNullOrWhiteSpace(criterionIdentifier) ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A not-applicable decision requires a criterion and a rationale of at most 4000 characters.");
        rationale = rationale.Trim();
        var decisionId = DecisionIdFor(Id, editionId, criterionIdentifier);
        var current = Read(decisionId);
        var revision = current?.Revision ?? 0;
        if (current?.Versions is [.., { Status: "proposed" } open])
        {
            // An exact retry of the proposal that is still pending returns its registration.
            if (expectedRevision + 1 == revision &&
                _proposers[(decisionId, open.VersionNumber)] == actorMemberId &&
                StringComparer.Ordinal.Equals(open.Rationale, rationale))
            {
                registration = new(decisionId, revision, open.VersionNumber);
                return null;
            }
            if (expectedRevision == revision)
                return CommandFailure.StateConflict(
                    "The criterion already has a pending not-applicable proposal awaiting review.");
        }
        if (expectedRevision != revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "criterion applicability decision", revision));
        if (current?.Status == "not_applicable")
            return CommandFailure.StateConflict(
                "The criterion is already not applicable. Withdraw the decision before proposing another.");
        var versionNumber = (current?.Versions.Count ?? 0) + 1;
        RaiseEvent(new CriterionNotApplicableProposed(_tenantId, Id, decisionId, revision + 1,
            versionNumber, editionId, criterionIdentifier, rationale, actorMemberId, actor,
            proposedAt));
        registration = new(decisionId, revision + 1, versionNumber);
        return null;
    }

    public CommandFailure? Review(Uuid decisionId, long expectedRevision, Uuid reviewDecisionId,
        string outcome, string rationale, Uuid reviewerMemberId, ActorReference reviewer,
        DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        if (Read(decisionId) is not { } current)
            return CommandFailure.MissingRecord("The criterion applicability decision was not found.");
        if (current.Versions[^1] is not { Status: "proposed" } pending)
            return CommandFailure.StateConflict(
                "The criterion has no pending not-applicable proposal to review.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "criterion applicability decision", current.Revision));
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(
                    SeparationOfDutiesRecordTypes.CriterionApplicability, decisionId,
                    decisionId, expectedRevision, SeparationOfDutiesActions.Review),
                reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and decision revision.");
        if (_proposers[(decisionId, pending.VersionNumber)] == reviewerMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "A not-applicable proposer cannot review their own proposal.");
        if (reviewDecisionId == Uuid.Empty || outcome is not ("accept" or "reject") ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "An applicability review requires an outcome of accept or reject and a rationale of at most 4000 characters.");
        RaiseEvent(new CriterionApplicabilityReviewed(_tenantId, Id, decisionId,
            current.Revision + 1, pending.VersionNumber, reviewDecisionId, outcome,
            rationale.Trim(), reviewer, reviewedAt, waiver?.Id));
        return null;
    }

    public CommandFailure? Withdraw(Uuid decisionId, long expectedRevision, string rationale,
        ActorReference actor, DateTimeOffset withdrawnAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Read(decisionId) is not { } current)
            return CommandFailure.MissingRecord("The criterion applicability decision was not found.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "criterion applicability decision", current.Revision));
        if (current.ActiveVersionNumber is not { } active ||
            current.Versions[^1].Status == "proposed")
            return CommandFailure.StateConflict(
                "Only an accepted not-applicable decision without a pending proposal can be withdrawn.");
        if (!IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "Withdrawing a not-applicable decision requires a rationale of at most 4000 characters.");
        RaiseEvent(new CriterionNotApplicableWithdrawn(_tenantId, Id, decisionId,
            current.Revision + 1, active, rationale.Trim(), actor, withdrawnAt));
        return null;
    }

    void Fold(Uuid decisionId, DomainEvent domainEvent) =>
        _decisions[decisionId] = CriterionApplicabilityReducer.Apply(
            _decisions.GetValueOrDefault(decisionId), domainEvent);

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;
}
