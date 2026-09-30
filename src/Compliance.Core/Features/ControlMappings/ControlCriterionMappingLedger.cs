using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Holds every control-to-criterion mapping of one program in one stream, so duplicate
///     resistance, review, and coverage reads are exact and replay is the only recovery step.
///     A mapping is keyed by (control, edition_id, criterion identifier); remapping to a later
///     control version or rationale adds a version that supersedes the accepted one on review.
/// </summary>
public sealed class ControlCriterionMappingLedger : Aggregate
{
    public const int MaximumTextLength = 4000;
    readonly Uuid _tenantId;
    readonly Dictionary<Uuid, MappingState> _mappings = [];
    readonly List<Uuid> _order = [];

    public ControlCriterionMappingLedger(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(),
            "control-criterion-mappings", programId.ToString()))
    {
        _tenantId = tenantId;
        On<ControlCriterionMappingProposed>(ev =>
        {
            if (!_mappings.TryGetValue(ev.MappingId, out var mapping))
            {
                mapping = new MappingState(ev.MappingId, ev.ControlId, ev.EditionId,
                    ev.CriterionIdentifier, ev.CriterionKind);
                _mappings.Add(ev.MappingId, mapping);
                _order.Add(ev.MappingId);
            }
            mapping.Revision = ev.Revision;
            mapping.Versions.Add(new VersionState(ev.VersionNumber, ev.ControlVersionId,
                ev.Rationale, ev.ApplicabilityExplanation, ev.Actor, ev.ActorMemberId,
                ev.ProposedAt));
            mapping.PendingVersion = ev.VersionNumber;
        });
        On<ControlCriterionMappingReviewed>(ev =>
        {
            var mapping = _mappings[ev.MappingId];
            mapping.Revision = ev.Revision;
            var version = mapping.Versions[ev.VersionNumber - 1];
            version.ReviewDecisionId = ev.DecisionId;
            version.ReviewedBy = ev.Actor;
            version.ReviewRationale = ev.Rationale;
            version.ReviewedAt = ev.DecidedAt;
            version.WaiverId = ev.SeparationOfDutiesWaiverId;
            mapping.PendingVersion = null;
            if (ev.Outcome == "accept")
            {
                if (mapping.ActiveVersion is { } previous)
                    mapping.Versions[previous - 1].Status = "superseded";
                version.Status = "accepted";
                mapping.ActiveVersion = ev.VersionNumber;
            }
            else
            {
                version.Status = "rejected";
            }
        });
        On<ControlCriterionMappingRetired>(ev =>
        {
            var mapping = _mappings[ev.MappingId];
            mapping.Revision = ev.Revision;
            var version = mapping.Versions[ev.VersionNumber - 1];
            version.Status = "retired";
            version.RetiredBy = ev.Actor;
            version.RetirementRationale = ev.Rationale;
            version.RetiredAt = ev.RetiredAt;
            mapping.ActiveVersion = null;
        });
    }

    public static Uuid MappingIdFor(Uuid programId, Uuid controlId, Uuid editionId,
        string criterionIdentifier) => Uuid.CreateVersion5(Uuid.CreateVersion5(programId,
        "control-criterion-mapping"), controlId + "|" + editionId + "|" + criterionIdentifier);

    public bool Contains(Uuid mappingId) => _mappings.ContainsKey(mappingId);

    public Uuid? EditionOf(Uuid mappingId) =>
        _mappings.TryGetValue(mappingId, out var mapping) ? mapping.EditionId : null;

    public bool HasPendingProposal(Uuid mappingId) =>
        _mappings.TryGetValue(mappingId, out var mapping) && mapping.PendingVersion is not null;

    public ControlCriterionMappingView? Read(Uuid mappingId) =>
        _mappings.TryGetValue(mappingId, out var mapping) ? View(mapping) : null;

    public IReadOnlyList<ControlCriterionMappingView> ReadAll() =>
        _order.Select(id => View(_mappings[id])).ToArray();

    public CommandFailure? Propose(Uuid controlId, Uuid controlVersionId, Uuid editionId,
        string criterionIdentifier, string criterionKind, long expectedRevision,
        string rationale, string applicabilityExplanation, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset proposedAt,
        out ControlCriterionMappingRegistration? registration)
    {
        registration = null;
        if (controlId == Uuid.Empty || controlVersionId == Uuid.Empty ||
            editionId == Uuid.Empty || string.IsNullOrWhiteSpace(criterionIdentifier) ||
            !IsBoundedText(rationale) || !IsBoundedText(applicabilityExplanation))
            return CommandFailure.InvalidContent(
                "A mapping requires a control version, catalog entry, rationale, and applicability explanation of at most 4000 characters each.");
        rationale = rationale.Trim();
        applicabilityExplanation = applicabilityExplanation.Trim();
        var mappingId = MappingIdFor(Id, controlId, editionId, criterionIdentifier);
        _mappings.TryGetValue(mappingId, out var mapping);
        var revision = mapping?.Revision ?? 0;
        if (mapping?.PendingVersion is { } pending)
        {
            var open = mapping.Versions[pending - 1];
            // An exact retry of the proposal that is still pending returns its registration.
            if (expectedRevision + 1 == revision && open.ProposerMemberId == actorMemberId &&
                open.Matches(controlVersionId, rationale, applicabilityExplanation))
            {
                registration = new(mappingId, revision, pending);
                return null;
            }
            if (expectedRevision == revision)
                return CommandFailure.StateConflict(
                    "The mapping already has a pending proposal awaiting review.");
        }
        if (expectedRevision != revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control criterion mapping", revision));
        if (mapping?.ActiveVersion is { } active &&
            mapping.Versions[active - 1].Matches(controlVersionId, rationale,
                applicabilityExplanation))
            return CommandFailure.StateConflict(
                "An accepted mapping with the same control version and explanation already exists.");
        var versionNumber = (mapping?.Versions.Count ?? 0) + 1;
        RaiseEvent(new ControlCriterionMappingProposed(_tenantId, Id, mappingId, revision + 1,
            versionNumber, controlId, controlVersionId, editionId, criterionIdentifier,
            criterionKind, rationale, applicabilityExplanation, actorMemberId, actorDisplay,
            proposedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        registration = new(mappingId, revision + 1, versionNumber);
        return null;
    }

    /// <summary>
    ///     The proposer may review only under an approved waiver scoped to
    ///     (control_criterion_mapping, mapping_id, mapping_id, expected_revision, review).
    /// </summary>
    public CommandFailure? Review(Uuid mappingId, long expectedRevision, Uuid decisionId,
        string outcome, string rationale, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver = null)
    {
        if (!_mappings.TryGetValue(mappingId, out var mapping))
            return CommandFailure.MissingRecord("The control criterion mapping was not found.");
        if (mapping.PendingVersion is not { } pending)
            return CommandFailure.StateConflict("The mapping has no pending proposal to review.");
        if (expectedRevision != mapping.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control criterion mapping", mapping.Revision));
        var proposal = mapping.Versions[pending - 1];
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlCriterionMapping,
                    mappingId, mappingId, expectedRevision, SeparationOfDutiesActions.Review),
                reviewerMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and mapping revision.");
        if (proposal.ProposerMemberId == reviewerMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "A mapping proposer cannot review their own proposal.");
        if (decisionId == Uuid.Empty || outcome is not ("accept" or "reject") ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A mapping review requires an outcome of accept or reject and a rationale of at most 4000 characters.");
        RaiseEvent(new ControlCriterionMappingReviewed(_tenantId, Id, mappingId,
            mapping.Revision + 1, pending, decisionId, outcome, rationale.Trim(),
            reviewerMemberId, reviewerDisplay, decidedAt, waiver?.Id)
        {
            StoredActor = ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
        });
        return null;
    }

    public CommandFailure? Retire(Uuid mappingId, long expectedRevision, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset retiredAt)
    {
        if (!_mappings.TryGetValue(mappingId, out var mapping))
            return CommandFailure.MissingRecord("The control criterion mapping was not found.");
        if (expectedRevision != mapping.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control criterion mapping", mapping.Revision));
        if (mapping.PendingVersion is not null)
            return CommandFailure.StateConflict(
                "Review the pending proposal before retiring the mapping.");
        if (mapping.ActiveVersion is not { } active)
            return CommandFailure.StateConflict("The mapping has no accepted version to retire.");
        if (!IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "Retiring a mapping requires a rationale of at most 4000 characters.");
        RaiseEvent(new ControlCriterionMappingRetired(_tenantId, Id, mappingId,
            mapping.Revision + 1, active, rationale.Trim(), actorMemberId, actorDisplay, retiredAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    ControlCriterionMappingView View(MappingState mapping)
    {
        var status = mapping.PendingVersion is not null ? "pending"
            : mapping.ActiveVersion is not null ? "active"
            : mapping.Versions.Any(static v => v.Status == "retired") ? "retired"
            : "rejected";
        var active = mapping.ActiveVersion is { } number ? mapping.Versions[number - 1] : null;
        return new ControlCriterionMappingView(_tenantId, Id, mapping.MappingId,
            mapping.ControlId, mapping.EditionId, mapping.Identifier, mapping.Kind,
            mapping.Revision, status, active?.Number, active?.ControlVersionId,
            mapping.Versions.Select(static v => v.View()).ToArray());
    }

    sealed class MappingState(Uuid mappingId, Uuid controlId, Uuid editionId, string identifier,
        string kind)
    {
        public Uuid MappingId { get; } = mappingId;
        public Uuid ControlId { get; } = controlId;
        public Uuid EditionId { get; } = editionId;
        public string Identifier { get; } = identifier;
        public string Kind { get; } = kind;
        public long Revision { get; set; }
        public int? PendingVersion { get; set; }
        public int? ActiveVersion { get; set; }
        public List<VersionState> Versions { get; } = [];
    }

    sealed class VersionState(int number, Uuid controlVersionId, string rationale,
        string explanation, ActorReference proposedBy, Uuid proposerMemberId,
        DateTimeOffset proposedAt)
    {
        public int Number { get; } = number;
        public Uuid ControlVersionId { get; } = controlVersionId;
        public Uuid ProposerMemberId { get; } = proposerMemberId;
        public string Status { get; set; } = "proposed";
        public Uuid? ReviewDecisionId { get; set; }
        public ActorReference? ReviewedBy { get; set; }
        public string? ReviewRationale { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public Uuid? WaiverId { get; set; }
        public ActorReference? RetiredBy { get; set; }
        public string? RetirementRationale { get; set; }
        public DateTimeOffset? RetiredAt { get; set; }

        public bool Matches(Uuid otherControlVersionId, string otherRationale,
            string otherExplanation) => ControlVersionId == otherControlVersionId &&
            StringComparer.Ordinal.Equals(rationale, otherRationale) &&
            StringComparer.Ordinal.Equals(explanation, otherExplanation);

        public ControlCriterionMappingVersionView View() => new(Number, ControlVersionId, Status,
            rationale, explanation, proposedBy, proposedAt, ReviewDecisionId, ReviewedBy,
            ReviewRationale, ReviewedAt, WaiverId, RetiredBy, RetirementRationale, RetiredAt);
    }
}
