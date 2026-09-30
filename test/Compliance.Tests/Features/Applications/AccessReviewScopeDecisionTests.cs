using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class AccessReviewScopeDecisionTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();
    static readonly Uuid RegistrantId = Uuid.CreateVersion4();
    static readonly Uuid LeadId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldSupersedeWithHistoryGivenLaterEffectiveDecision()
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);

        // Act
        var included = scope.Decide(instance, 0, Uuid.CreateVersion4(), "included",
            " Production data ", Now, Now.AddYears(1), LeadId, "Lead", Now, null);
        var excluded = scope.Decide(instance, 1, Uuid.CreateVersion4(), "excluded",
            "Decommissioned workload", Now.AddDays(30), null, LeadId, "Lead", Now, null);

        // Assert
        Assert.True(included.IsSuccess);
        Assert.True(excluded.IsSuccess);
        Assert.Equal("included", scope.ToView(ApplicationId, Now.AddDays(1)).Status);
        var later = scope.ToView(ApplicationId, Now.AddDays(31));
        Assert.Equal("excluded", later.Status);
        Assert.Equal(2, later.Decisions.Count);
        Assert.Equal("Production data", later.Decisions[0].Reason);
        Assert.Equal("unresolved", scope.ToView(ApplicationId, Now.AddDays(-1)).Status);
        var events = new AggregateScenario<SystemInstanceAccessReviewScope>(scope).PendingEvents;
        Assert.Equal([1L, 2L], events.Cast<AccessReviewScopeDecided>().Select(ev => ev.Sequence));
        Assert.All(events.Cast<AccessReviewScopeDecided>(),
            ev => Assert.Equal(1, ev.SystemInstanceRevision));
    }

    [Fact]
    public void ShouldReturnRecordedDecisionGivenRetriedDecisionId()
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);
        var decisionId = Uuid.CreateVersion4();
        Assert.True(scope.Decide(instance, 0, decisionId, "included", "In boundary", Now,
            null, LeadId, "Lead", Now, null).IsSuccess);

        // Act
        var retry = scope.Decide(instance, 0, decisionId, "included", "In boundary", Now,
            null, LeadId, "Lead", Now, null);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Single(scope.Decisions);
    }

    [Theory]
    [InlineData("maybe", "Reason", 0, 0, RequestErrorKind.Validation)]
    [InlineData("excluded", " ", 0, 0, RequestErrorKind.Validation)]
    [InlineData("included", "Reason", 1, 0, RequestErrorKind.Conflict)]
    [InlineData("included", "Reason", 0, -1, RequestErrorKind.Validation)]
    public void ShouldRejectDecisionGivenInvalidOrStaleInput(string decision, string reason,
        long expectedCount, int reviewByDays, RequestErrorKind expected)
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);
        DateTimeOffset? reviewBy = reviewByDays == 0 ? null : Now.AddDays(reviewByDays);

        // Act
        var result = scope.Decide(instance, expectedCount, Uuid.CreateVersion4(), decision,
            reason, Now, reviewBy, LeadId, "Lead", Now, null);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scope.Decisions);
    }

    [Fact]
    public void ShouldRejectBackdatedSupersessionGivenPriorDecision()
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);
        Assert.True(scope.Decide(instance, 0, Uuid.CreateVersion4(), "included", "In boundary",
            Now, null, LeadId, "Lead", Now, null).IsSuccess);

        // Act
        var result = scope.Decide(instance, 1, Uuid.CreateVersion4(), "excluded", "Rewrite",
            Now.AddDays(-1), null, LeadId, "Lead", Now, null);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldProhibitRegistrantGivenNoWaiver()
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);

        // Act
        var result = scope.Decide(instance, 0, Uuid.CreateVersion4(), "excluded", "Sandbox",
            Now, null, RegistrantId, "Registrant", Now, null);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scope.Decisions);
    }

    [Fact]
    public void ShouldAllowRegistrantGivenExactScopeWaiver()
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);
        var waiver = ApprovedWaiver(RegistrantId, new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.SystemInstanceAccessReviewScope, instance.Id,
            instance.Id, instance.Revision, SeparationOfDutiesActions.Approve));

        // Act
        var result = scope.Decide(instance, 0, Uuid.CreateVersion4(), "included", "Prod",
            Now, null, RegistrantId, "Registrant", Now, waiver);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(waiver.Id, result.Value.SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectWaiverGivenNoConflict()
    {
        // Arrange
        var instance = Instance();
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);
        var waiver = ApprovedWaiver(LeadId, new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.SystemInstanceAccessReviewScope, instance.Id,
            instance.Id, instance.Revision, SeparationOfDutiesActions.Approve));

        // Act
        var result = scope.Decide(instance, 0, Uuid.CreateVersion4(), "included", "Prod",
            Now, null, LeadId, "Lead", Now, waiver);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldRejectDecisionGivenRetiredInstance()
    {
        // Arrange
        var instance = Instance();
        Assert.Null(instance.Retire(1, Now, "Account closed", RegistrantId, "Registrant", Now));
        var scope = new SystemInstanceAccessReviewScope(TenantId, instance.Id);

        // Act
        var result = scope.Decide(instance, 0, Uuid.CreateVersion4(), "excluded", "Closed",
            Now, null, LeadId, "Lead", Now, null);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static DeclaredSystemInstance Instance()
    {
        var instance = new DeclaredSystemInstance(TenantId, Uuid.CreateVersion4());
        Assert.True(instance.Declare(ApplicationId, "AWS production", "cloud_account", null,
            "123456789012", RegistrantId, "Registrant", Now.AddDays(-5)).IsSuccess);
        return instance;
    }

    static SeparationOfDutiesWaiver ApprovedWaiver(Uuid beneficiary,
        SeparationOfDutiesWaiverScope scope)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Admin",
            "Small team", Now.AddDays(-2), Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Second admin", Now.AddDays(-1)));
        return waiver;
    }
}
