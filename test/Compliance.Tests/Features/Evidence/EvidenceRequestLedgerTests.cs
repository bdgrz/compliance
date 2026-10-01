using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceRequestLedgerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid OwnerMemberId = Uuid.CreateVersion4();
    static readonly ActorReference Lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Due = new(2026, 10, 15);

    [Fact]
    public void ShouldOpenAnAttributedRequestGivenValidDetails()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);
        var requestId = Uuid.CreateVersion4();

        // Act
        var failure = Open(ledger, requestId);

        // Assert
        Assert.Null(failure);
        var request = ledger.Find(requestId)!;
        Assert.Equal(EvidenceRequestLedger.Open, request.Status);
        Assert.Equal(1, request.Revision);
        Assert.Equal(OwnerMemberId, request.OwnerMemberId);
        Assert.Equal(Due, request.DueOn);
        Assert.Equal(Lead, request.RequestedBy);
        Assert.IsType<EvidenceRequestOpened>(Assert.Single(
            new AggregateScenario<EvidenceRequestLedger>(ledger).PendingEvents));
    }

    [Theory]
    [InlineData(" ", "Upload the export", 0)]
    [InlineData("Access export", " ", 0)]
    [InlineData("Access export", "Upload the export", -1)]
    public void ShouldRejectBeforeAppendGivenInvalidDetails(string title, string instructions, int dueOffsetDays)
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);

        // Act
        var failure = ledger.OpenRequest(Uuid.CreateVersion4(), title, instructions, OwnerMemberId,
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(dueOffsetDays), null, Lead, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceRequestLedger>(ledger).PendingEvents);
    }

    [Fact]
    public void ShouldReplayIdenticalOpenGivenSameRequestId()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);
        var requestId = Uuid.CreateVersion4();
        Assert.Null(Open(ledger, requestId));

        // Act
        var replay = Open(ledger, requestId);

        // Assert
        Assert.Null(replay);
        Assert.Single(new AggregateScenario<EvidenceRequestLedger>(ledger).PendingEvents);
    }

    [Fact]
    public void ShouldFulfilWithAnArtifactGivenTheCurrentRevision()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);
        var requestId = Uuid.CreateVersion4();
        Assert.Null(Open(ledger, requestId));
        var artifactId = Uuid.CreateVersion4();

        // Act
        var failure = ledger.Fulfil(requestId, 1, artifactId, Lead, Now.AddHours(1));

        // Assert
        Assert.Null(failure);
        var request = ledger.Find(requestId)!;
        Assert.Equal(EvidenceRequestLedger.Fulfilled, request.Status);
        Assert.Equal(artifactId, request.ArtifactId);
        Assert.Equal(2, request.Revision);
    }

    [Fact]
    public void ShouldRejectStaleRevisionGivenConcurrentChange()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);
        var requestId = Uuid.CreateVersion4();
        Assert.Null(Open(ledger, requestId));

        // Act
        var failure = ledger.Fulfil(requestId, 2, Uuid.CreateVersion4(), Lead, Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldRefuseFulfilmentGivenCancelledRequest()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);
        var requestId = Uuid.CreateVersion4();
        Assert.Null(Open(ledger, requestId));
        Assert.Null(ledger.Cancel(requestId, 1, "No longer needed", Lead, Now));

        // Act
        var failure = ledger.Fulfil(requestId, 2, Uuid.CreateVersion4(), Lead, Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(EvidenceRequestLedger.Cancelled, ledger.Find(requestId)!.Status);
    }

    [Fact]
    public void ShouldRequireRationaleGivenCancellation()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);
        var requestId = Uuid.CreateVersion4();
        Assert.Null(Open(ledger, requestId));

        // Act
        var failure = ledger.Cancel(requestId, 1, " ", Lead, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldReportMissingRecordGivenUnknownRequest()
    {
        // Arrange
        var ledger = new EvidenceRequestLedger(TenantId, ProgramId);

        // Act
        var failure = ledger.Fulfil(Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), Lead, Now);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(failure).Code);
    }

    static CommandFailure? Open(EvidenceRequestLedger ledger, Uuid requestId) =>
        ledger.OpenRequest(requestId, "Q3 access review export", "Upload the Okta admin export.", OwnerMemberId, Due,
            null, Lead, Now);
}
