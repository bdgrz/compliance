using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportRetirementPlanTests
{
    [Fact]
    public void ShouldReject201ProofRowsGiven200SourceRowsAndOneRetirement()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var actor = Uuid.CreateVersion4();
        var rows = Enumerable.Range(1, ApplicationImportLedger.MaximumCommitEffectRows)
            .Select(index => new ApplicationImportInputRow($"new-{index}", $"Application {index}",
                "Observed purpose", null)).ToArray();
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications",
            "declared_complete", rows);
        var staged = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(staged.Stage(request, actor, "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var batch = new ImportBatch(tenant, staged.Id);
        _ = new AggregateScenario<ImportBatch>(batch).Given(new AggregateScenario<ImportBatch>(staged).PendingEvents.ToArray());
        foreach (var row in batch.GetRows())
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
                ledger.GetRevision(batch), "create_new", null, null, "Reviewed new identity"), null,
                actor, "Lead", DateTimeOffset.UtcNow));
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var correlated = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(correlated).Given(history.ToArray());
        Assert.True(correlated.FreezeRetirementProposal(batch, correlated.GetRevision(batch),
            correlated.CommittedStreamPosition, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            actor, "Lead", "Reviewed source omission", DateTimeOffset.UtcNow).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(correlated).PendingEvents);
        var proposalLedger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(proposalLedger).Given(history.ToArray());
        var proposal = Assert.IsType<ApplicationImportRetirementProposal>(proposalLedger.GetRetirementProposal(batch));
        var impact = new ApplicationChangePreview(tenant, target.Id, target.Revision, "retire", [], [], [], true)
        { ImpactDigest = new string('a', 64) };
        var before = new AggregateScenario<ApplicationImportLedger>(proposalLedger).PendingEvents.Count;
        var targetEffectsBefore = target.GetPendingImportEffects().Count;

        // Act
        var result = proposalLedger.BeginAcceptance(batch, proposal.Revision,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, actor, "Lead",
            DateTimeOffset.UtcNow, new Dictionary<Uuid, ApplicationChangePreview> { [target.Id] = impact });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(proposalLedger).PendingEvents.Count);
        Assert.Equal(targetEffectsBefore, target.GetPendingImportEffects().Count);
        Assert.Null(proposalLedger.GetFrozenPlan(batch.Id));
    }

    [Fact]
    public void ShouldIncludeRetirementGivenSealedProposalAndCompleteCurrentImpact()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        var row = Assert.Single(batch.GetRows());
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            row.RowId, 1, "create_new", null, null, "Reviewed new application"), null,
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var saved = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(history.ToArray());
        Assert.True(saved.FreezeRetirementProposal(batch, 2, saved.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", "Reviewed omission", DateTimeOffset.UtcNow).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(saved).PendingEvents);
        var frozen = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(frozen).Given(history.ToArray());
        var proposal = Assert.IsType<ApplicationImportRetirementProposal>(frozen.GetRetirementProposal(batch));
        var impact = new ApplicationChangePreview(tenant, target.Id, target.Revision, "retire",
            [], [], [], true)
        { ImpactDigest = new string('a', 64) };

        // Act
        var result = frozen.PrepareAcceptancePlan(batch, proposal.Revision,
            new Dictionary<Uuid, ApplicationChangePreview> { [target.Id] = impact });

        // Assert
        Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error.Message);
        var retirement = Assert.Single(result.Value, row => row.Decision == "retire");
        Assert.Equal(target.Id, retirement.ApplicationId);
        Assert.Equal(target.Revision, retirement.ExpectedApplicationRevision);
    }

    [Fact]
    public void ShouldRejectAcceptanceGivenCompetingSourceEffectAfterRetirementProposal()
    {
        // Arrange
        var (tenant, source, target, batch, proposal, impact) = PrepareFrozenRetirement();
        var competing = PrepareCompetingLink(tenant, target);
        Assert.True(target.RecordPendingImportEffect(competing.Ledger, competing.Batch,
            competing.Row.RowId, DateTimeOffset.UtcNow).IsSuccess);
        var before = new AggregateScenario<ApplicationImportLedger>(source).PendingEvents.Count;

        // Act
        var result = source.BeginAcceptance(batch, proposal.Revision,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow,
            new Dictionary<Uuid, ApplicationChangePreview> { [target.Id] = impact });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(source).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectRetirementReservationGivenCompetingSourceEffectAfterAcceptance()
    {
        // Arrange
        var (tenant, source, target, batch, proposal, impact) = PrepareFrozenRetirement();
        Assert.True(source.BeginAcceptance(batch, proposal.Revision,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow,
            new Dictionary<Uuid, ApplicationChangePreview> { [target.Id] = impact }).IsSuccess);
        var frozenRow = Assert.Single(source.GetFrozenPlan(batch.Id)!.Rows,
            row => row.Decision == "retire");
        var competing = PrepareCompetingLink(tenant, target);
        Assert.True(target.RecordPendingImportEffect(competing.Ledger, competing.Batch,
            competing.Row.RowId, DateTimeOffset.UtcNow).IsSuccess);
        var before = new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count;

        // Act
        var result = target.RecordPendingImportEffect(source, batch, frozenRow.RowId, DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.Equal(before, new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectCompetingSourceEffectGivenUnsettledRetirementReservation()
    {
        // Arrange
        var (tenant, source, target, batch, proposal, impact) = PrepareFrozenRetirement();
        Assert.True(source.BeginAcceptance(batch, proposal.Revision,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow,
            new Dictionary<Uuid, ApplicationChangePreview> { [target.Id] = impact }).IsSuccess);
        var retirement = Assert.Single(source.GetFrozenPlan(batch.Id)!.Rows,
            row => row.Decision == "retire");
        Assert.True(target.RecordPendingImportEffect(source, batch, retirement.RowId,
            DateTimeOffset.UtcNow).IsSuccess);
        var competing = PrepareCompetingLink(tenant, target);
        var before = new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count;

        // Act
        var result = target.RecordPendingImportEffect(competing.Ledger, competing.Batch,
            competing.Row.RowId, DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.Equal(before, new AggregateScenario<DeclaredApplication>(target).PendingEvents.Count);
    }

    [Fact]
    public void ShouldFreezeProposalGivenCompleteSourceOmission()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        target.ResolveImportVisibility(ledger);
        var position = ledger.CommittedStreamPosition;

        // Act
        var result = ledger.FreezeRetirementProposal(batch, 1, position,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", "Source no longer includes application", DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(target.IsRetired);
        Assert.Equal("preview_ready", ledger.GetState(batch));
    }

    [Fact]
    public void ShouldExposeImmutableProposalGivenDurableSeal()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, observed) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        var actor = Uuid.CreateVersion4();
        Assert.True(Freeze(ledger, batch, target, actor).IsSuccess);
        Assert.Null(ledger.GetRetirementProposal(batch));
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var saved = new ApplicationImportLedger(tenant, "manual", "applications");

        // Act
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(history.ToArray());
        var proposal = Assert.IsType<ApplicationImportRetirementProposal>(saved.GetRetirementProposal(batch));

        // Assert
        var row = Assert.Single(proposal.Rows);
        Assert.Equal(observed.Id, row.LastObservedBatchId);
        Assert.Equal(target.Id, row.ApplicationId);
        Assert.Equal(1, row.ExpectedApplicationRevision);
        Assert.Equal(Uuid.CreateVersion5(saved.Id, "application_import_claim:app-1"), row.SourceClaimId);
        Assert.Equal(actor, proposal.Start.MemberId);
        Assert.Equal("Lead", proposal.Start.MemberDisplay);
        Assert.Equal("Reviewed omission", proposal.Start.Reason);
        Assert.Equal(64, proposal.ProposalSha256.Length);
        Assert.Throws<NotSupportedException>(() => ((IList<ApplicationImportRetirementRow>)proposal.Rows).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)proposal.Start.PresentSourceRecordIds).Clear());
        Assert.False(saved.PrepareAcceptancePlan(batch, proposal.Revision).IsSuccess);
        Assert.False(target.IsRetired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldPreserveOriginalAttributionGivenProposalReplay(bool changedActor)
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        var sourcePosition = ledger.CommittedStreamPosition;
        var actor = Uuid.CreateVersion4();
        Assert.True(Freeze(ledger, batch, target, actor).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var saved = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(history.ToArray());
        var before = saved.GetRetirementProposal(batch);

        // Act
        var replay = saved.FreezeRetirementProposal(batch, 1, sourcePosition,
            new Dictionary<Uuid, DeclaredApplication>(), changedActor ? Uuid.CreateVersion4() : actor,
            "Lead", "Reviewed omission", DateTimeOffset.UtcNow.AddDays(1));

        // Assert
        Assert.Equal(!changedActor, replay.IsSuccess);
        Assert.Same(before, saved.GetRetirementProposal(batch));
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(saved).PendingEvents);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("correlate")]
    [InlineData("other_batch")]
    public void ShouldInvalidateProposalGivenLaterSourceLifecycleChange(string change)
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        Assert.True(Freeze(ledger, batch, target).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var saved = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(history.ToArray());
        var revision = saved.GetRevision(batch);

        // Act
        if (change == "cancel")
            Assert.Null(saved.Cancel(batch, revision, "Cancel", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        else if (change == "correlate")
            Assert.Null(saved.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
                Assert.Single(batch.GetRows()).RowId, revision, "create_new", null, null, "Reviewed identity"),
                null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        else
        {
            var other = Stage(tenant, "app-3", "Other batch");
            Assert.Null(saved.Cancel(other, 1, "Cancel", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        }

        // Assert
        Assert.Null(saved.GetRetirementProposal(batch));
        Assert.False(target.IsRetired);
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("retired")]
    [InlineData("missing")]
    [InlineData("tenant")]
    [InlineData("identity")]
    [InlineData("unsettled")]
    public void ShouldRejectProposalGivenUnusableGovernedTarget(string condition)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        if (condition != "unsettled")
            target.ResolveImportVisibility(ledger);
        if (condition == "changed")
            Assert.Null(target.Revise(1, "Changed", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        if (condition == "retired")
            Assert.Null(target.Retire(1, DateTimeOffset.UtcNow, "Reviewed", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var replacement = condition == "tenant" ? new DeclaredApplication(Uuid.CreateVersion4(), target.Id)
            : condition == "identity" ? new DeclaredApplication(tenant, Uuid.CreateVersion4()) : target;
        if (replacement != target)
            Assert.True(replacement.Declare("Other", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var targets = new Dictionary<Uuid, DeclaredApplication> { [target.Id] = replacement };
        if (condition == "missing")
            targets.Clear();
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");

        // Act
        var result = ledger.FreezeRetirementProposal(batch, 1, ledger.CommittedStreamPosition, targets,
            Uuid.CreateVersion4(), "Lead", "Reviewed omission", DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("invalid")]
    [InlineData("revision")]
    [InlineData("position")]
    [InlineData("tenant")]
    [InlineData("no_omissions")]
    [InlineData("canceled")]
    [InlineData("actor")]
    [InlineData("reason")]
    public void ShouldRejectProposalGivenInvalidPreparationInput(string condition)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        target.ResolveImportVisibility(ledger);
        var batch = Stage(condition == "tenant" ? Uuid.CreateVersion4() : tenant,
            condition == "no_omissions" ? "app-1" : "app-2", condition == "invalid" ? "" : "Another source record",
            condition == "partial" ? "partial" : "declared_complete");
        if (condition == "canceled")
            Assert.Null(ledger.Cancel(batch, 1, "Cancel", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var result = ledger.FreezeRetirementProposal(batch, condition == "revision" ? 2 : 1,
            condition == "position" ? ledger.CommittedStreamPosition - 1 : ledger.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            condition == "actor" ? Uuid.Empty : Uuid.CreateVersion4(), "Lead",
            condition == "reason" ? new string('x', 2001) : "Reviewed omission", DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData("row_identity")]
    [InlineData("source_identity")]
    [InlineData("target")]
    [InlineData("provenance")]
    [InlineData("revision")]
    [InlineData("seal")]
    [InlineData("attribution")]
    [InlineData("incomplete")]
    [InlineData("duplicate")]
    public void ShouldRejectReplayGivenAlteredRetirementProposal(string corruption)
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        Assert.True(Freeze(ledger, batch, target).IsSuccess);
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToList();
        var start = (ApplicationImportRetirementProposalStarted)events[0];
        var frozen = (ApplicationImportRetirementRowFrozen)events[1];
        var seal = (ApplicationImportRetirementProposalSealed)events[2];
        events[1] = corruption switch
        {
            "row_identity" => frozen with { Row = frozen.Row with { SourceClaimId = Uuid.CreateVersion4() } },
            "source_identity" => frozen with { Row = frozen.Row with { SourceRecordId = "APP-1" } },
            "target" => frozen with { Row = frozen.Row with { ApplicationId = Uuid.CreateVersion4() } },
            "provenance" => frozen with { Row = frozen.Row with { LastObservedBatchId = Uuid.CreateVersion4() } },
            "revision" => frozen with { Row = frozen.Row with { ExpectedApplicationRevision = 2 } },
            _ => frozen
        };
        if (corruption == "attribution")
            events[0] = start with { MemberId = Uuid.CreateVersion4() };
        if (corruption == "seal")
            events[2] = seal with { ProposalSha256 = new string('0', 64) };
        if (corruption == "incomplete")
            events.RemoveAt(1);
        if (corruption == "duplicate")
            events.Insert(2, frozen with { Revision = frozen.Revision + 1 });
        var replay = new ApplicationImportLedger(tenant, "manual", "applications");

        // Act
        var act = () => new AggregateScenario<ApplicationImportLedger>(replay).Given([.. history, .. events]);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
        Assert.Null(replay.GetRetirementProposal(batch));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectProposalGivenSharedSourceTarget(bool aliasPresent)
    {
        // Arrange
        var ledgerHistory = new List<DomainEvent>();
        var targetHistory = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: ledgerHistory, targetHistory: targetHistory);
        target.ResolveImportVisibility(ledger);
        (ledger, target) = AddCommittedClaim(tenant, ledger, target, "app-2", ledgerHistory, targetHistory);
        var batch = Stage(tenant, aliasPresent ? "app-2" : "app-3", "Later source", "declared_complete");

        // Act
        var result = Freeze(ledger, batch, target);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        Assert.False(target.IsRetired);
    }

    [Fact]
    public void ShouldRejectReplayGivenReorderedDistinctRetirementRows()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var other = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(other.Declare("Other", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var targetHistory = new AggregateScenario<DeclaredApplication>(other).PendingEvents.ToList();
        (ledger, other) = AddCommittedClaim(tenant, ledger, other, "app-2", history, targetHistory);
        var batch = Stage(tenant, "app-3", "Later source", "declared_complete");
        Assert.True(ledger.FreezeRetirementProposal(batch, 1, ledger.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target, [other.Id] = other },
            Uuid.CreateVersion4(), "Lead", "Reviewed omission", DateTimeOffset.UtcNow).IsSuccess);
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToList();
        var first = (ApplicationImportRetirementRowFrozen)events[1];
        var second = (ApplicationImportRetirementRowFrozen)events[2];
        events[1] = first with { Row = second.Row };
        events[2] = second with { Row = first.Row };
        var replay = new ApplicationImportLedger(tenant, "manual", "applications");

        // Act
        var act = () => new AggregateScenario<ApplicationImportLedger>(replay).Given([.. history, .. events]);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    static (ApplicationImportLedger Ledger, DeclaredApplication Target) AddCommittedClaim(Uuid tenant,
        ApplicationImportLedger ledger, DeclaredApplication target, string sourceRecordId,
        List<DomainEvent> ledgerHistory, List<DomainEvent> targetHistory)
    {
        var batch = Stage(tenant, sourceRecordId, "Observed alias");
        var row = Assert.Single(batch.GetRows());
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
            1, "link_existing", target.Id, 1, "Reviewed identity"), target, actor, "Lead", now));
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            actor, "Lead", now).IsSuccess);
        ledgerHistory.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var accepting = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(accepting).Given(ledgerHistory.ToArray());
        Assert.True(target.RecordPendingImportEffect(accepting, batch, row.RowId, now).IsSuccess);
        targetHistory.AddRange(new AggregateScenario<DeclaredApplication>(target).PendingEvents.Where(ev =>
            targetHistory.All(previous => previous.Metadata.EventId != ev.Metadata.EventId)));
        var savedTarget = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(savedTarget).Given(targetHistory.ToArray());
        Assert.True(accepting.Commit(batch, 5, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = savedTarget }, now).IsSuccess);
        ledgerHistory.AddRange(new AggregateScenario<ApplicationImportLedger>(accepting).PendingEvents);
        var savedLedger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(savedLedger).Given(ledgerHistory.ToArray());
        savedTarget.ResolveImportVisibility(savedLedger);
        return (savedLedger, savedTarget);
    }

    [Fact]
    public void ShouldRejectProposalGivenPresentRowNewlyCorrelatedToMissingTarget()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Present alias", "declared_complete");
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            Assert.Single(batch.GetRows()).RowId, 1, "link_existing", target.Id, 1, "Reviewed alias"),
            target, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var saved = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(saved).Given(history.ToArray());

        // Act
        var result = saved.FreezeRetirementProposal(batch, 2, saved.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", "Reviewed omission", DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(saved).PendingEvents);
        Assert.False(target.IsRetired);
    }

    [Fact]
    public async Task ShouldKeepPreviewReadyAtLatestRevisionGivenRetirementProposalProjection()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Later source", "declared_complete");
        Assert.True(Freeze(ledger, batch, target).IsSuccess);
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents;
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationImportDirectoryV1", EventStreamPattern.ForPattern(tenant.ToString()));
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationImportStaged(tenant, batch.Id, Uuid.CreateVersion4(),
                "manual", "applications", "declared_complete", batch.ContentDigest!, batch.GetRows(),
                Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            foreach (var ev in events)
                await directory.ApplyAsync(ev);
            await projection.CommitAsync(ProjectionCheckpoint.Start);
        }
        var view = await directory.GetAsync(tenant, batch.Id);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(4, view.Revision);
        Assert.Equal("preview_ready", view.State);
        Assert.Equal(0, view.PendingCount);
        Assert.Equal(0, view.AppliedCount);
    }

    static Result Freeze(ApplicationImportLedger ledger, ImportBatch batch, DeclaredApplication target, Uuid? actor = null) =>
        ledger.FreezeRetirementProposal(batch, 1, ledger.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, actor ?? Uuid.CreateVersion4(),
            "Lead", "Reviewed omission", DateTimeOffset.UtcNow);

    static ImportBatch Stage(Uuid tenant, string sourceRecordId, string name, string coverage = "partial",
        string sourceKey = "manual")
    {
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), sourceKey, "applications", coverage,
            [new(sourceRecordId, name, "Observed purpose", "Observed owner")]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var saved = new ImportBatch(tenant, batch.Id);
        _ = new AggregateScenario<ImportBatch>(saved).Given(new AggregateScenario<ImportBatch>(batch).PendingEvents.ToArray());
        return saved;
    }

    static (Uuid Tenant, ApplicationImportLedger Ledger, DeclaredApplication Target, ImportBatch Batch) Committed(
        bool persistCommit = true, List<DomainEvent>? ledgerHistory = null, List<DomainEvent>? targetHistory = null)
    {
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var batch = Stage(tenant, "app-1", "Observed name");
        var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Governed name", "Governed purpose", null, actor, "Lead", now).IsSuccess);
        var plan = new ApplicationImportLedger(tenant, "manual", "applications");
        var row = Assert.Single(batch.GetRows());
        Assert.Null(plan.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
            1, "link_existing", target.Id, 1, "Reviewed identity"), target, actor, "Lead", now));
        var targets = new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target };
        Assert.True(plan.BeginAcceptance(batch, 2, targets, actor, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var planEvents = new AggregateScenario<ApplicationImportLedger>(plan).PendingEvents.ToArray();
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(planEvents);
        Assert.True(target.RecordPendingImportEffect(ledger, batch, row.RowId, now).IsSuccess);
        targetHistory?.AddRange(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
        var savedTarget = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(savedTarget).Given(
            new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
        Assert.True(ledger.Commit(batch, 5, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = savedTarget }, now).IsSuccess);
        if (!persistCommit)
            return (tenant, ledger, savedTarget, batch);
        ledgerHistory?.AddRange([.. planEvents, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        var savedLedger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(savedLedger).Given(
            [.. planEvents, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        return (tenant, savedLedger, savedTarget, batch);
    }

    static (Uuid Tenant, ApplicationImportLedger Source, DeclaredApplication Target,
        ImportBatch Batch, ApplicationImportRetirementProposal Proposal, ApplicationChangePreview Impact)
        PrepareFrozenRetirement()
    {
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        target.ResolveImportVisibility(ledger);
        var batch = Stage(tenant, "app-2", "Another source record", "declared_complete");
        var row = Assert.Single(batch.GetRows());
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            row.RowId, 1, "create_new", null, null, "Reviewed new application"), null,
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var correlated = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(correlated).Given(history.ToArray());
        Assert.True(correlated.FreezeRetirementProposal(batch, 2, correlated.CommittedStreamPosition,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, Uuid.CreateVersion4(),
            "Lead", "Reviewed omission", DateTimeOffset.UtcNow).IsSuccess);
        history.AddRange(new AggregateScenario<ApplicationImportLedger>(correlated).PendingEvents);
        var source = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(source).Given(history.ToArray());
        var proposal = Assert.IsType<ApplicationImportRetirementProposal>(source.GetRetirementProposal(batch));
        var impact = new ApplicationChangePreview(tenant, target.Id, target.Revision, "retire",
            [], [], [], true)
        { ImpactDigest = new string('a', 64) };
        return (tenant, source, target, batch, proposal, impact);
    }

    static (ApplicationImportLedger Ledger, ImportBatch Batch, ApplicationImportStagedRow Row)
        PrepareCompetingLink(Uuid tenant, DeclaredApplication target)
    {
        var batch = Stage(tenant, "competitor", "Competing import", sourceKey: "other");
        var row = Assert.Single(batch.GetRows());
        var ledger = new ApplicationImportLedger(tenant, "other", "applications");
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            row.RowId, 1, "link_existing", target.Id, target.Revision, "Reviewed current target"),
            target, actor, "Lead", now));
        Assert.True(ledger.BeginAcceptance(batch, 2,
            new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target }, actor, "Lead", now).IsSuccess);
        return (ledger, batch, row);
    }
}
