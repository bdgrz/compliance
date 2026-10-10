using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportOmissionConsumerTests
{
    [Fact]
    public async Task ShouldRetainBlockedProposalGivenCurrentManagerConfirmsExactPreview()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, original, application) = await fixture.SeedAsync();

        // Act
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, batchId), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var request = Freeze(preview.Value);
        var result = await SendAsync(fixture, request, "http");
        var read = await fixture.Bus.SendAsync(new GetApplicationImportOmissionProposal(fixture.Tenant, batchId), fixture.Actor);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, application, CancellationToken.None);
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, batchId));
        var ledger = await LedgerAsync(fixture);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(read.IsSuccess, read.Error?.Message);
        var row = Assert.Single(read.Value.MissingRows);
        Assert.Equal(original, row.LastObservedBatchId);
        Assert.Equal(application, row.ApplicationId);
        Assert.Equal(preview.Value.SourcePosition, read.Value.SourcePosition);
        Assert.Equal(preview.Value.ContentSha256, read.Value.ContentSha256);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User), read.Value.PreparerMemberId);
        Assert.Equal("Confirmed source omission", read.Value.Reason);
        Assert.Equal(64, read.Value.ProposalSha256.Length);
        Assert.Contains("missing_source_retirement_unavailable", read.Value.AcceptanceBlockers);
        Assert.Contains("retirement_impact_unavailable", read.Value.AcceptanceBlockers);
        Assert.False(target.IsRetired);
        Assert.Equal(1, target.Revision);
        Assert.Equal("preview_ready", ledger.GetState(batch));
        Assert.False(ledger.PrepareAcceptancePlan(batch, read.Value.Revision).IsSuccess);
    }

    [Fact]
    public async Task ShouldKeepAcceptancePendingGivenProductionImpactContextsAreUnresolved()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, applicationId) = await fixture.SeedAsync();
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(
            fixture.Tenant, batchId), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.True((await SendAsync(fixture, Freeze(preview.Value), "http")).IsSuccess);
        var proposal = await fixture.Bus.SendAsync(new GetApplicationImportOmissionProposal(
            fixture.Tenant, batchId), fixture.Actor);
        Assert.True(proposal.IsSuccess, proposal.Error?.Message);
        var before = await LedgerAsync(fixture);

        // Act
        var result = await SendAsync(fixture, new AcceptApplicationImport(fixture.Tenant,
            batchId, proposal.Value.Revision), "http");
        var after = await LedgerAsync(fixture);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, applicationId,
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Contains("complete current impact", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Null(after.GetFrozenPlan(batchId));
        Assert.False(target.IsRetired);
        Assert.Equal(1, target.Revision);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldDenyConfirmationGivenNonHttpPersonalInvocation(string transport)
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, _) = await fixture.SeedAsync();
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var before = await LedgerAsync(fixture);

        // Act
        var result = await SendAsync(fixture, Freeze(preview.Value), transport);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("over HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
    }

    [Theory]
    [InlineData("stage", RequestErrorKind.Forbidden)]
    [InlineData("nonmember", RequestErrorKind.NotFound)]
    [InlineData("suspended", RequestErrorKind.NotFound)]
    [InlineData("deprovisioned", RequestErrorKind.NotFound)]
    [InlineData("firm_staff", RequestErrorKind.Forbidden)]
    [InlineData("foreign", RequestErrorKind.NotFound)]
    public async Task ShouldDenyProposalReadsAndWritesGivenCurrentManagerAuthorityLost(string denial, RequestErrorKind kind)
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, _) = await fixture.SeedAsync();
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        fixture.Permissions.StageOnly = denial == "stage";
        fixture.Memberships.State = denial;
        var tenant = denial == "foreign" ? Uuid.CreateVersion4() : fixture.Tenant;
        var before = await LedgerAsync(fixture);

        // Act
        var read = await fixture.Bus.SendAsync(new GetApplicationImportOmissionProposal(tenant, id), fixture.Actor);
        var nextPreview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(tenant, id), fixture.Actor);
        var write = await SendAsync(fixture, Freeze(preview.Value) with { TenantId = tenant }, "http");

        // Assert
        Assert.Equal(kind, read.Error?.Kind);
        Assert.Equal(kind, nextPreview.Error?.Kind);
        Assert.Equal(kind, write.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveReadButDenyConfirmationGivenPermanentAttestHistory()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, _) = await fixture.SeedAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, revoked: true);
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        var before = await LedgerAsync(fixture);

        // Act
        var result = await SendAsync(fixture, Freeze(preview.Value), "http");

        // Assert
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("actual Attest assignment history", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
    }

    [Theory]
    [InlineData("position")]
    [InlineData("digest")]
    [InlineData("revision")]
    [InlineData("target")]
    [InlineData("source")]
    [InlineData("alias")]
    public async Task ShouldPreserveSourceGivenChangedConfirmationFacts(string change)
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, targetId) = await fixture.SeedAsync();
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var request = Freeze(preview.Value);
        if (change == "position")
            request = request with { ExpectedSourcePosition = request.ExpectedSourcePosition + 1 };
        if (change == "digest")
            request = request with { ExpectedContentSha256 = new string('0', 64) };
        if (change == "revision")
            request = request with { ExpectedBatchRevision = request.ExpectedBatchRevision + 1 };
        if (change == "target")
            Assert.True((await fixture.Bus.SendAsync(new ReviseApplication(fixture.Tenant, targetId, 1, "Changed", "Purpose", null), fixture.Actor)).IsSuccess);
        if (change is "source" or "alias")
        {
            var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, id));
            var row = Assert.Single(batch.GetRows());
            Assert.True((await SendAsync(fixture, new CorrelateApplicationImportRow(fixture.Tenant, id, row.RowId, 1,
                change == "alias" ? "link_existing" : "create_new", change == "alias" ? targetId : null,
                change == "alias" ? 1 : null, "Reviewed correlation"), "http")).IsSuccess);
            if (change == "alias")
            {
                preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
                request = Freeze(preview.Value);
                Assert.Contains("source_alias_cannot_propose_retirement", preview.Value.AcceptanceBlockers);
            }
        }
        var before = await LedgerAsync(fixture);

        // Act
        var result = await SendAsync(fixture, request, "http");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
        Assert.False((await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, targetId, CancellationToken.None)).IsRetired);
    }

    [Theory]
    [InlineData("partial", false)]
    [InlineData("declared_complete", true)]
    public async Task ShouldPreserveBlockedSourceGivenIncompleteOrInvalidInput(string coverage, bool invalid)
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, _) = await fixture.SeedAsync(coverage, invalid);
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var before = await LedgerAsync(fixture);

        // Act
        var result = await SendAsync(fixture, Freeze(preview.Value), "http");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
        if (coverage == "partial")
            Assert.Empty(preview.Value.MissingRows);
    }

    [Fact]
    public async Task ShouldKeepExactSealGivenRetryAndRejectChangedIntentOrLaterSourceAdvance()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, _) = await fixture.SeedAsync();
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        var request = Freeze(preview.Value);
        var metadata = RequestMetadata.Create();
        Assert.True((await SendAsync(fixture, request, "http", metadata)).IsSuccess);
        var before = await LedgerAsync(fixture);

        // Act
        var retry = await SendAsync(fixture, request, "http", metadata);
        var changed = await SendAsync(fixture, request with { Reason = "Changed intent" }, "http", metadata);
        var read = await fixture.Bus.DispatchAsync(new GetApplicationImportOmissionProposal(fixture.Tenant, id),
            new RequestDispatchContext(fixture.Actor, new McpInvocation("synthetic.omission.get")), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
        var other = await fixture.Bus.SendAsync(new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(),
            "manual", "applications", "partial", [new("other", "Other", "Purpose", null)]), fixture.Actor);
        Assert.True(other.IsSuccess, other.Error?.Message);
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, other.Value.BatchId));
        Assert.True((await SendAsync(fixture, new CorrelateApplicationImportRow(fixture.Tenant, batch.Id,
            Assert.Single(batch.GetRows()).RowId, 1, "create_new", null, null, "New correlation"), "http")).IsSuccess);
        var stale = await fixture.Bus.SendAsync(new GetApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(stale.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldDenyProposalWithoutAppendGivenOtherSourceUnsettledTargetEffect()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (id, _, targetId) = await fixture.SeedAsync();
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, id), fixture.Actor);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var other = await fixture.Bus.SendAsync(new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(),
            "second", "applications", "partial", [new("pending", "Pending observation", "Purpose", null)]), fixture.Actor);
        Assert.True(other.IsSuccess, other.Error?.Message);
        var batch = await fixture.Reader.HydrateAsync(new ImportBatch(fixture.Tenant, other.Value.BatchId));
        var row = Assert.Single(batch.GetRows());
        Assert.True((await SendAsync(fixture, new CorrelateApplicationImportRow(fixture.Tenant, batch.Id,
            row.RowId, 1, "link_existing", targetId, 1, "Second source"), "http")).IsSuccess);
        Assert.True((await SendAsync(fixture, new AcceptApplicationImport(fixture.Tenant, batch.Id, 2), "http")).IsSuccess);
        Assert.True((await fixture.Bus.SendAsync(new ApplyApplicationImportEffect(fixture.Tenant, batch.Id,
            row.RowId, targetId), RequestActor.System)).IsSuccess);
        var before = await LedgerAsync(fixture);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, targetId, CancellationToken.None);
        Assert.NotNull(target.CheckPendingImportChanges());

        // Act
        var result = await SendAsync(fixture, Freeze(preview.Value), "http");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("The retirement target changed or has unsettled effects.", result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, (await LedgerAsync(fixture)).CommittedStreamPosition);
        Assert.False(target.IsRetired);
    }

    static FreezeApplicationImportOmissionProposal Freeze(ApplicationImportOmissionPreview view) =>
        new(view.TenantId, view.BatchId, view.Revision, view.SourcePosition, view.ContentSha256, "Confirmed source omission");

    static ValueTask<Result> SendAsync(ApplicationImportCompositionTests.Fixture fixture,
        IRequest request, string transport, RequestMetadata? metadata = null) =>
        fixture.Bus.DispatchAsync(request, new RequestDispatchContext(fixture.Actor,
            transport == "http" ? new HttpInvocation("POST", "/synthetic/import/omission", "/synthetic/import/omission", "synthetic")
                : transport == "mcp" ? new McpInvocation("synthetic.import.omission") : new DirectInvocation(), metadata), CancellationToken.None);

    static ValueTask<ApplicationImportLedger> LedgerAsync(ApplicationImportCompositionTests.Fixture fixture) =>
        fixture.Reader.HydrateAsync(new ApplicationImportLedger(fixture.Tenant, "manual", "applications"));
}
