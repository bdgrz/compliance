using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Tests.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ServiceEngagementAcceptanceTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid ClientUser = Uuid.CreateVersion4();
    static readonly ActorReference ClientActor = ActorReference.ForMember(RbacIds.Member(Tenant, ClientUser), "Synthetic client administrator");
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldCommitActualHistoryGivenVerifiedSyntheticAcceptanceAndRatifiedFixtureRules()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now);
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray()).Aggregate;
        var history = replay.Acceptance(proof.EngagementId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(history);
        Assert.Equal("accepted", replay.Engagement(proof.EngagementId)!.Status);
        Assert.True(Assert.Single(history.Assignments).IsCurrent);
        Assert.Equal(proof.ReviewTaskId, history.ReviewTaskId);
        Assert.False(replay.Engagement(proof.EngagementId)!.ProfessionalAccessGranted);
    }

    [Fact]
    public void ShouldRefuseProductionAcceptanceGivenUnratifiedFixtureRules()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules with { IsRatified = false }, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(ledger.Acceptance(proof.EngagementId));
        Assert.Equal(2, ledger.Sequence);
    }

    [Fact]
    public void ShouldRefuseAcceptanceGivenSelfConsistentApprovedBoundaryDifferentFromSelectedDraft()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        var foreignVersion = Uuid.CreateVersion4();
        proof = proof with
        {
            Boundary = proof.Boundary! with { VersionId = foreignVersion },
            BoundaryApproval = proof.BoundaryApproval! with { VersionId = foreignVersion }
        };

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, ledger.Sequence);
    }

    [Fact]
    public void ShouldPreservePermanentIdentityWallGivenAcceptedEngagementClosed()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("advisory");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var original = proof.CurrentStaff[0];

        // Act
        var closed = ledger.CloseEngagement(Uuid.CreateVersion4(), proof.EngagementId, 3,
            "Client ended engagement", ClientActor, Now);
        var alias = original with { StaffMemberId = Uuid.CreateVersion4(), Practice = "attest" };
        var attempted = ledger.CreateEngagement(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 4,
            new ServiceEngagementDraftContent("attest", "New scope", new DateOnly(2026, 1, 1), null, alias.StaffMemberId),
            alias, ClientActor, Now);

        // Assert
        Assert.True(closed.IsSuccess);
        Assert.Equal("closed", ledger.Acceptance(proof.EngagementId)!.Status);
        Assert.False(ledger.IsEligibleForProfessionalAccess(proof.EngagementId, original.StaffMemberId, original.UserId, 1, 1, Now));
        Assert.Equal(RequestErrorKind.Conflict, attempted.Error?.Kind);
        Assert.Contains(IndependenceCompartments.PersonExclusivityRuleId, attempted.Error!.Message, StringComparison.Ordinal);
        Assert.Single(ledger.ActualAssignmentHistory);
    }

    [Fact]
    public void ShouldDenyManagementAuthorshipGivenActualAttestHistoryEvenWithClientIdentity()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var staff = proof.CurrentStaff[0];
        var engagement = Uuid.CreateVersion4();
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, 3,
            new ServiceEngagementDraftContent("attest", "New draft", new DateOnly(2026, 1, 1), null, staff.StaffMemberId),
            staff, ClientActor, Now).IsSuccess);
        var actor = ActorReference.ForMember(RbacIds.Member(Tenant, staff.UserId), "Synthetic client-role alias");

        // Act
        var result = ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
            Tenant, engagement, Uuid.CreateVersion4(), 4, 1, [], "I retain management responsibility."), staff.UserId, actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(ledger.ManagementAcknowledgements(engagement));
        Assert.Equal(4, ledger.Sequence);
    }

    [Fact]
    public void ShouldLoseDenyOnlyEligibilityGivenNewServiceFactsOrChangedExternalVersions()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var staff = proof.CurrentStaff[0];
        Assert.True(ledger.IsEligibleForProfessionalAccess(proof.EngagementId, staff.StaffMemberId, staff.UserId, 1, 1, Now));
        Assert.True(ledger.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3,
            new NonattestServiceContent(proof.EngagementId, "readiness", new DateOnly(2026, 1, 1), null,
                [staff.StaffMemberId], false, "New synthetic service fact"), ClientActor, Now).IsSuccess);

        // Act
        var staleFacts = ledger.IsEligibleForProfessionalAccess(proof.EngagementId, staff.StaffMemberId, staff.UserId, 1, 1, Now);
        var staleRules = ledger.IsEligibleForProfessionalAccess(proof.EngagementId, staff.StaffMemberId, staff.UserId, 2, 1, Now);
        var staleStaff = ledger.IsEligibleForProfessionalAccess(proof.EngagementId, staff.StaffMemberId, staff.UserId, 1, 2, Now);

        // Assert
        Assert.False(staleFacts);
        Assert.False(staleRules);
        Assert.False(staleStaff);
        Assert.False(ledger.Engagement(proof.EngagementId)!.ProfessionalAccessGranted);
    }

    [Theory]
    [InlineData("partner")]
    [InlineData("staff")]
    [InlineData("boundary")]
    public void ShouldConflictOnRetryGivenChangedAcceptanceAuthorityOrSource(string source)
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        var requestId = Uuid.CreateVersion4();
        Assert.True(ledger.AcceptEngagement(requestId, 2, proof, rules, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(events).Aggregate;
        var changed = source switch
        {
            "partner" => proof with { PartnerUserId = Uuid.CreateVersion4() },
            "staff" => proof with { CurrentStaff = [proof.CurrentStaff[0] with { Revision = 2 }] },
            _ => proof with
            {
                Boundary = proof.Boundary! with { Revision = 2 },
                BoundaryApproval = proof.BoundaryApproval! with { Revision = 2 }
            }
        };

        // Act
        var original = replay.AcceptEngagement(requestId, 2, proof, rules, Now.AddDays(1));
        var conflicting = replay.AcceptEngagement(requestId, 2, changed, rules, Now);

        // Assert
        Assert.True(original.IsSuccess);
        Assert.Equal(Now, original.Value!.RecordedAt);
        Assert.Equal(RequestErrorKind.Conflict, conflicting.Error?.Kind);
        Assert.Equal(3, replay.Sequence);
    }

    [Theory]
    [InlineData("impairing", true, false)]
    [InlineData("conditionally_compatible", false, false)]
    [InlineData("conditionally_compatible", true, true)]
    [InlineData("compatible", true, true)]
    public void ShouldComputeFullRetainedServicePolicyGivenCurrentAcknowledgementAndEvaluation(string classification,
        bool evaluation, bool expectedAllowed)
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        var serviceId = Uuid.CreateVersion4();
        Assert.True(ledger.RecordService(Uuid.CreateVersion4(), serviceId, 2,
            new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2025, 12, 1), null,
                [Uuid.CreateVersion4()], false, "Synthetic policy service"), ClientActor, Now).IsSuccess);
        var acknowledgement = Uuid.CreateVersion4();
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(Tenant,
            proof.EngagementId, acknowledgement, 3, 1, [serviceId], "I retain management responsibility."), ClientUser, ClientActor, Now).IsSuccess);
        proof = proof with
        {
            ManagementAcknowledgementId = acknowledgement,
            PartnerEvaluationReference = evaluation ? "Synthetic recorded partner evaluation" : null
        };
        rules = rules with
        {
            Content = rules.Content with
            {
                ServiceRules =
            [new IndependenceServiceRuleContent("readiness", classification, "impairing")]
            }
        };

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 4, proof, rules, Now);

        // Assert
        Assert.Equal(expectedAllowed, result.IsSuccess);
        if (expectedAllowed)
            Assert.Equal(serviceId, Assert.Single(result.Value!.CompleteServiceHistory).ServiceRecordId);
        else
            Assert.Null(ledger.Acceptance(proof.EngagementId));
    }

    [Fact]
    public void ShouldRevokeCurrentEligibilityWithoutErasingHistoryGivenActualLeadRemoved()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("advisory");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var staff = proof.CurrentStaff[0];

        // Act
        var result = ledger.RemoveActualStaff(Uuid.CreateVersion4(), proof.EngagementId, staff.StaffMemberId,
            3, "Client revoked professional access", ClientActor, Now);
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray()).Aggregate;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("closed", replay.Acceptance(proof.EngagementId)!.Status);
        Assert.Single(replay.ActualAssignmentHistory);
        var history = replay.AcceptanceHistory(proof.EngagementId);
        Assert.Equal(2, history.Count);
        Assert.True(Assert.Single(history[0].Assignments).IsCurrent);
        Assert.Equal(ClientActor, history[1].ChangedBy);
        Assert.Equal("Client revoked professional access", history[1].ChangeReason);
        Assert.False(replay.IsEligibleForProfessionalAccess(proof.EngagementId, staff.StaffMemberId, staff.UserId, 1, 1, Now));
        Assert.False(Assert.Single(replay.Acceptance(proof.EngagementId)!.Assignments).IsCurrent);
    }

    [Theory]
    [InlineData("request_identity")]
    [InlineData("partner_revision")]
    [InlineData("boundary")]
    [InlineData("facts")]
    [InlineData("outcome")]
    [InlineData("assignment")]
    public void ShouldRejectAlteredAcceptedEvidenceGivenMalformedReplay(string corruption)
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var ev = Assert.IsType<ServiceEngagementAcceptanceRecorded>(events[^1]);
        if (corruption == "request_identity")
            ev = ev with { RequestId = Assert.IsType<ServiceEngagementMutationRecorded>(events[0]).RequestId };
        var acceptance = ev.Acceptance;
        acceptance = corruption switch
        {
            "request_identity" => acceptance,
            "partner_revision" => acceptance with { PartnerDirectoryStaffRevision = 0 },
            "boundary" => acceptance with { BoundaryVersionId = Uuid.CreateVersion4() },
            "facts" => acceptance with
            {
                CompleteServiceHistory = [new NonattestServiceView(Tenant,
                Uuid.CreateVersion4(), new NonattestServiceContent(proof.EngagementId, "readiness",
                    new DateOnly(2026, 1, 1), null, [Uuid.CreateVersion4()], false, "Invented synthetic source"), ClientActor, Now)]
            },
            "outcome" => acceptance with { EvaluationOutcome = "impaired" },
            _ => acceptance with { Assignments = [acceptance.Assignments[0] with { UserId = Uuid.CreateVersion4() }] }
        };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            events.Take(events.Length - 1).Append(ev with { Acceptance = acceptance }).ToArray());

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldAcceptUnscopedEngagementGivenNoSelectedBoundaryOrBoundaryProof()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        var staff = proof.CurrentStaff[0];
        var content = ledger.Engagement(proof.EngagementId)!.Content with { ExaminationBoundary = null };
        Assert.True(ledger.AmendEngagement(Uuid.CreateVersion4(), proof.EngagementId, 2, content, staff, ClientActor, Now).IsSuccess);
        var acknowledgement = Uuid.CreateVersion4();
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(Tenant,
            proof.EngagementId, acknowledgement, 3, 2, [], "I retain management responsibility."), ClientUser, ClientActor, Now).IsSuccess);
        proof = proof with
        {
            ReviewedDraftRevision = 2,
            ManagementAcknowledgementId = acknowledgement,
            Boundary = null!,
            BoundaryApproval = null!
        };

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 4, proof, rules, Now);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ShouldRefuseRevocationGivenDecisionPredatesRecordedAcceptance()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);

        // Act
        var result = ledger.CloseEngagement(Uuid.CreateVersion4(), proof.EngagementId, 3,
            "Synthetic chronology violation", ClientActor, Now.AddDays(-1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(3, ledger.Sequence);
        Assert.Equal("active", ledger.Acceptance(proof.EngagementId)!.Status);
    }

    [Theory]
    [InlineData(2025, 12, 31)]
    [InlineData(2027, 1, 1)]
    public void ShouldDenyEligibilityGivenCurrentTimeOutsideEngagementPeriod(int year, int month, int day)
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var staff = proof.CurrentStaff[0];

        // Act
        var eligible = ledger.IsEligibleForProfessionalAccess(proof.EngagementId, staff.StaffMemberId, staff.UserId,
            1, 1, new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));

        // Assert
        Assert.False(eligible);
    }

    [Fact]
    public void ShouldRefuseAcceptanceGivenDecisionPredatesItsRetainedDraftAndAcknowledgement()
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now.AddDays(-1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(ledger.Acceptance(proof.EngagementId));
        Assert.Equal(2, ledger.Sequence);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("closed")]
    public void ShouldRejectLegacyDraftMutationGivenAlreadyAcceptedEngagement(string state)
    {
        // Arrange
        var (ledger, proof, rules) = Fixture("attest");
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var original = Assert.IsType<ServiceEngagementMutationRecorded>(events[0]);
        var current = ledger.Engagement(proof.EngagementId)!;
        var forged = original with
        {
            RequestId = Uuid.CreateVersion4(),
            ExpectedSequence = 3,
            Engagement = current with
            {
                Revision = 3,
                Status = state,
                Actor = ClientActor,
                Staff = state == "closed" ? current.Staff.Select(staff => staff with
                { IsCurrent = false, ProposalState = "withdrawn" }).ToArray() : current.Staff
            }
        };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(events.Append(forged).ToArray());

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldReadRetainedAcceptanceGivenProductionComposedClientAdministrationBus()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", ClientUser.ToString())], "BdgrzSession"));
        var seeded = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new IndependenceLedger(Tenant), ledger =>
            {
                var (_, proof, rules) = Fixture("attest", ledger);
                return AggregateOutcome.CommitOnSuccess(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2, proof, rules, Now));
            }, new RequestDispatchContext(actor));
        Assert.True(seeded.IsSuccess);
        var engagement = seeded.Value!.EngagementId;
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var current = await bus.SendAsync(new GetServiceEngagementAcceptance(Tenant, engagement), actor);
        var history = await bus.SendAsync(new GetServiceEngagementAcceptanceHistory(Tenant, engagement), actor);

        // Assert
        Assert.True(current.IsSuccess);
        Assert.True(history.IsSuccess);
        Assert.Equal(engagement, current.Value!.EngagementId);
        Assert.Equal(JsonSerializer.Serialize(current.Value, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView),
            JsonSerializer.Serialize(Assert.Single(history.Value!), ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView));
        Assert.Equal("active", current.Value.Status);
        Assert.Equal("allowed", current.Value.DecisionCode);
    }

    [Fact]
    public void ShouldRefuseBackdatedAcceptanceGivenLaterTimestampInRetainedDraftHistory()
    {
        // Arrange
        var earlier = Now.AddDays(-1);
        var (ledger, proof, rules) = Fixture("attest", sourceRecordedAt: earlier);
        var staff = proof.CurrentStaff[0];
        Assert.True(ledger.AmendEngagement(Uuid.CreateVersion4(), proof.EngagementId, 2,
            ledger.Engagement(proof.EngagementId)!.Content, staff, ClientActor, earlier).IsSuccess);
        var acknowledgement = Uuid.CreateVersion4();
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(Tenant,
            proof.EngagementId, acknowledgement, 3, 2, [], "I retain management responsibility."), ClientUser, ClientActor, earlier).IsSuccess);
        proof = proof with { ReviewedDraftRevision = 2, ManagementAcknowledgementId = acknowledgement };

        // Act
        var result = ledger.AcceptEngagement(Uuid.CreateVersion4(), 4, proof, rules, earlier);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(4, ledger.Sequence);
        Assert.Null(ledger.Acceptance(proof.EngagementId));
    }

    [Fact]
    public void ShouldRejectBackdatedReplayGivenLaterTimestampInRetainedDraftHistory()
    {
        // Arrange
        var earlier = Now.AddDays(-1);
        var (ledger, proof, rules) = Fixture("attest", sourceRecordedAt: earlier);
        Assert.True(ledger.AmendEngagement(Uuid.CreateVersion4(), proof.EngagementId, 2,
            ledger.Engagement(proof.EngagementId)!.Content, proof.CurrentStaff[0], ClientActor, earlier).IsSuccess);
        var acknowledgement = Uuid.CreateVersion4();
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(Tenant,
            proof.EngagementId, acknowledgement, 3, 2, [], "I retain management responsibility."), ClientUser, ClientActor, earlier).IsSuccess);
        proof = proof with { ReviewedDraftRevision = 2, ManagementAcknowledgementId = acknowledgement };
        Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), 4, proof, rules, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var ev = Assert.IsType<ServiceEngagementAcceptanceRecorded>(events[^1]);
        var corrupt = ev with
        {
            Acceptance = ev.Acceptance with
            {
                RecordedAt = earlier,
                Assignments = ev.Acceptance.Assignments.Select(staff => staff with { AssignedAt = earlier }).ToArray()
            }
        };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Tenant)).Given(
            events.Take(events.Length - 1).Append(corrupt).ToArray());

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    static (IndependenceLedger Ledger, VerifiedEngagementAcceptance Proof, IndependenceRuleVersionView Rules) Fixture(string practice, IndependenceLedger? source = null, DateTimeOffset? sourceRecordedAt = null)
    {
        var ledger = source ?? new IndependenceLedger(Tenant);
        var sourceTime = sourceRecordedAt ?? Now;
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), practice,
            "Synthetic directory source", true, 1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), sourceTime);
        var engagement = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var content = new ServiceEngagementDraftContent(practice, "Synthetic scope", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), staff.StaffMemberId,
            new ServiceEngagementBoundaryReference(boundaryId, versionId, 1));
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, 0, content, staff, ClientActor, Now).IsSuccess);
        var acknowledgement = Uuid.CreateVersion4();
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
            Tenant, engagement, acknowledgement, 1, 1, [], "I retain management responsibility."), ClientUser, ClientActor, Now).IsSuccess);
        var boundary = new BoundaryVersionView(Tenant, boundaryId, Uuid.CreateVersion4(), versionId, 1,
            new BoundaryContent("Synthetic approved scope", "examination", ["security"], []), "approved",
            new DateOnly(2026, 1, 1), Uuid.CreateVersion4(), "Synthetic author", sourceTime);
        var approval = new BoundaryDecisionView(Tenant, boundaryId, approvalId, versionId, 1, "approve",
            Uuid.CreateVersion4(), "Synthetic reviewer", "Synthetic approval", sourceTime, null, null, "synthetic-digest");
        var partner = staff with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() };
        var proof = new VerifiedEngagementAcceptance(Tenant, engagement, 1, Uuid.CreateVersion4(),
            partner.StaffMemberId, partner.UserId, "Synthetic verified authority ONLY; not production evidence", null,
            acknowledgement, boundary, approval, [staff], partner, 1, sourceTime);
        var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic ratified test fixture ONLY; not production evidence"),
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), sourceTime, true);
        return (ledger, proof, rules);
    }
}
