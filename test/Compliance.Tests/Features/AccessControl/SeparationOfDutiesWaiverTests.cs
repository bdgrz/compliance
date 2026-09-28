using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class SeparationOfDutiesWaiverTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid WaiverId = Uuid.CreateVersion4();
    static readonly Uuid RequesterId = Uuid.CreateVersion4();
    static readonly Uuid BeneficiaryId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset RequestedAt = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    static readonly SeparationOfDutiesWaiverScope Scope = new(
        SeparationOfDutiesRecordTypes.Boundary,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3,
        SeparationOfDutiesActions.Review);

    [Fact]
    public void ShouldRecordExactScopeAndRequireIndependentOrgAdminApprovalGivenWaiverRequest()
    {
        // Arrange
        var waiver = new SeparationOfDutiesWaiver(TenantId, WaiverId);

        // Act
        var recorded = waiver.Record(Scope, BeneficiaryId, RequesterId, "Org Admin",
            "Only available reviewer this week.", RequestedAt, RequestedAt.AddDays(7));
        var approvedByRequester = waiver.Approve(RequesterId, "Org Admin",
            RequestedAt.AddMinutes(1));
        var approvedByBeneficiary = waiver.Approve(BeneficiaryId, "Beneficiary",
            RequestedAt.AddMinutes(2));
        var approved = waiver.Approve(ApproverId, "Second Org Admin", RequestedAt.AddMinutes(3));

        // Assert
        Assert.Null(recorded);
        Assert.Equal(CommandFailureCode.ActorProhibited, approvedByRequester?.Code);
        Assert.Equal(CommandFailureCode.ActorProhibited, approvedByBeneficiary?.Code);
        Assert.Null(approved);
        var events = new AggregateScenario<SeparationOfDutiesWaiver>(waiver).PendingEvents;
        Assert.Collection(events,
            request =>
            {
                var ev = Assert.IsType<SeparationOfDutiesWaiverRecorded>(request);
                Assert.Equal(Scope, ev.Scope);
                Assert.Equal(BeneficiaryId, ev.BeneficiaryMemberId);
                Assert.Equal(RequesterId, ev.RequesterMemberId);
                Assert.Equal(RequestedAt.AddDays(7), ev.ExpiresAt);
                Assert.Equal("Only available reviewer this week.", ev.Rationale);
            },
            approval =>
            {
                var ev = Assert.IsType<SeparationOfDutiesWaiverApproved>(approval);
                Assert.Equal(ApproverId, ev.ApproverMemberId);
                Assert.Equal(RequestedAt.AddMinutes(3), ev.ApprovedAt);
            });
        Assert.True(waiver.Allows(Scope, BeneficiaryId, RequestedAt.AddDays(1)));
    }

    [Fact]
    public void ShouldRejectInvalidOrExpiredWaiverGivenScopeMismatch()
    {
        // Arrange
        var waiver = new SeparationOfDutiesWaiver(TenantId, WaiverId);
        Assert.Null(waiver.Record(Scope, BeneficiaryId, RequesterId, "Org Admin",
            "The only reviewer is unavailable.", RequestedAt, RequestedAt.AddDays(1)));
        Assert.Null(waiver.Approve(ApproverId, "Second Org Admin", RequestedAt.AddMinutes(1)));

        // Act
        var differentRevision = waiver.Allows(Scope with { Revision = Scope.Revision + 1 },
            BeneficiaryId, RequestedAt.AddMinutes(2));
        var differentBeneficiary = waiver.Allows(Scope, RequesterId, RequestedAt.AddMinutes(2));
        var expired = waiver.Allows(Scope, BeneficiaryId, RequestedAt.AddDays(1));
        var validAtRequestTime = waiver.Allows(Scope, BeneficiaryId, RequestedAt);

        // Assert
        Assert.False(differentRevision);
        Assert.False(differentBeneficiary);
        Assert.False(expired);
        Assert.False(validAtRequestTime);
    }

    [Fact]
    public void ShouldRejectSelfApprovalAndAlreadyExpiredRequestGivenWaiver()
    {
        // Arrange
        var selfBeneficiary = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        var expired = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());

        // Act
        var selfApproval = selfBeneficiary.Record(Scope, RequesterId, RequesterId,
            "Org Admin", "Only admin available.", RequestedAt, RequestedAt.AddDays(1));
        Assert.Null(selfApproval);
        var denied = selfBeneficiary.Approve(RequesterId, "Org Admin", RequestedAt.AddMinutes(1));
        var expiredRequest = expired.Record(Scope, BeneficiaryId, RequesterId,
            "Org Admin", "Too late.", RequestedAt, RequestedAt);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, denied?.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, expiredRequest?.Code);
        Assert.Empty(new AggregateScenario<SeparationOfDutiesWaiver>(expired).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveRequestTermsGivenWaiverRequestReplay()
    {
        // Arrange
        var waiver = new SeparationOfDutiesWaiver(TenantId, WaiverId);
        Assert.Null(waiver.Record(Scope, BeneficiaryId, RequesterId, "Org Admin",
            "No alternate reviewer is available.", RequestedAt, RequestedAt.AddDays(1)));

        // Act
        var replay = waiver.Record(Scope, BeneficiaryId, RequesterId, "Org Admin",
            "No alternate reviewer is available.", RequestedAt.AddMinutes(1),
            RequestedAt.AddDays(1));
        var changedTerms = waiver.Record(Scope with { Action = SeparationOfDutiesActions.Approve },
            BeneficiaryId, RequesterId, "Org Admin", "No alternate reviewer is available.",
            RequestedAt.AddMinutes(1), RequestedAt.AddDays(1));

        // Assert
        Assert.Null(replay);
        Assert.Equal(CommandFailureCode.StateConflict, changedTerms?.Code);
        Assert.Single(new AggregateScenario<SeparationOfDutiesWaiver>(waiver).PendingEvents);
    }
}
