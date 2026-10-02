using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class CriteriaTextOverlayLedgerTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic author");
    static readonly CriteriaTextOverlayContent Content = new("Synthetic supplied text", "Test supplier",
        "Test license", new CriteriaOverlayUsageFlags(true, false));

    [Fact]
    public void ShouldRetainRevisionAndOriginalDecisionGivenReplayAndRetryAfterRestriction()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var source = new CriteriaTextOverlayLedger(tenant, edition);
        Assert.True(source.Set("CC1.1", requestId, null, Content, Author, DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(source.Set("CC1.1", Uuid.CreateVersion4(), 1,
            Content with { UsageFlags = new CriteriaOverlayUsageFlags(false, false) }, Author,
            DateTimeOffset.UtcNow).IsSuccess);
        var events = new AggregateScenario<CriteriaTextOverlayLedger>(source).PendingEvents.ToArray();

        // Act
        var hydrated = new AggregateScenario<CriteriaTextOverlayLedger>(new CriteriaTextOverlayLedger(tenant, edition))
            .Given(events).Aggregate;
        var retry = hydrated.Set("CC1.1", requestId, null, Content, Author, DateTimeOffset.UtcNow);
        var conflict = hydrated.Set("CC1.1", requestId, null, Content with { Text = "Different intent" }, Author,
            DateTimeOffset.UtcNow);
        var stale = hydrated.Set("CC1.1", Uuid.CreateVersion4(), 1, Content, Author, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(2, events.Length);
        Assert.Equal(2, hydrated.Get("CC1.1")!.Revision);
        Assert.Equal(1, retry.Value!.Revision);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Empty(new AggregateScenario<CriteriaTextOverlayLedger>(hydrated).PendingEvents);
        Assert.Equal(Content.Text, Assert.IsType<CriteriaTextOverlayEntryRevised>(events[0]).Content.Text);
    }

    [Theory]
    [InlineData("", "Supplier", "License")]
    [InlineData("Text", "", "License")]
    [InlineData("Text", "Supplier", "")]
    [InlineData("Text", "Supplier\u0001", "License")]
    public void ShouldRejectOverlayGivenMissingOrInvalidSourceFacts(string text, string supplier, string license)
    {
        // Arrange
        var source = new CriteriaTextOverlayLedger(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = source.Set("CC1.1", Uuid.CreateVersion4(), null,
            new CriteriaTextOverlayContent(text, supplier, license, new CriteriaOverlayUsageFlags(false, false)),
            Author, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CriteriaTextOverlayLedger>(source).PendingEvents);
    }
}
