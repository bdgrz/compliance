using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceRedactionCompositionTests
{
    [Fact]
    public async Task ShouldRetainExactLineageGivenRealSourcesAndDeliberateArtifactGrants()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, derived), artifact =>
            artifact.Register(new EvidenceArtifactContent("Manually redacted derivative", "Declared derivative", "manual",
                    "Independent existing content", ArtifactMetadataAccessTests.Fixture.Now, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "restricted"),
                new string('b', 64), 30, ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Collector"),
                ArtifactMetadataAccessTests.Fixture.Now));
        Assert.True((await fixture.DispatchAsync(fixture.Grant(derived, BuiltInRbac.ViewerRoleId(fixture.TenantId)))).IsSuccess);
        await fixture.ProjectAsync();
        var request = new PrepareEvidenceRedaction(fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual replacement", "Remove private employee fields");
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId),
                new HttpInvocation("POST", "/test", "/test", "test")));
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new EvidenceRedaction(fixture.TenantId, request.RedactionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, retained.Revision);
        Assert.Equal(fixture.ArtifactId, retained.CurrentPreparation!.Original.ArtifactId);
        Assert.Equal(derived, retained.CurrentPreparation.Derived.ArtifactId);
        Assert.Equal(result.Value.Preparations[0].PreparationId, retained.CurrentPreparation.PreparationId);
    }
    [Fact]
    public async Task ShouldRetainExactPersonalApprovalGivenIndependentCurrentLeadAndAvailableDerivative()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = await SeedDerivedAsync(fixture);
        var contributor = await SeedMemberAsync(fixture, BuiltInRbac.StandardUsersTeamId(fixture.TenantId));
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, contributor, derived);
        await GrantPairAsync(fixture, lead, derived);
        var prepared = await DispatchAsync(fixture, contributor, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual replacement", "Remove private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var preparation = prepared.Value.Preparations[0];

        // Act
        var approved = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(fixture.TenantId,
            prepared.Value.RedactionId, 1, preparation.PreparationId, preparation.Revision));
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(approved.Value.ApprovalCurrent);
        Assert.Equal(RbacIds.Member(fixture.TenantId, contributor).ToString(), retained.CurrentPreparation!.PreparedBy.Id);
        Assert.Equal(RbacIds.Member(fixture.TenantId, lead).ToString(), retained.CurrentApproval!.ApprovedBy.Id);
        Assert.Equal(preparation.PreparationId, retained.CurrentApproval.PreparationId);
    }

    [Fact]
    public async Task ShouldReturnPublicLineageGivenCurrentArtifactAccess()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);

        // Act
        var read = await DispatchAsync(fixture, fixture.UserId,
            new GetEvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId), new DirectInvocation());

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(prepared.Value.Preparations, read.Value.Preparations);
        Assert.False(read.Value.ApprovalCurrent);
    }

    [Fact]
    public async Task ShouldDenyRecordingUnverifiedScopeWithoutAppendGivenMissingRedactionPreparation()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var request = new RecordSeparationOfDutiesWaiver(fixture.TenantId,
            new SeparationOfDutiesWaiverScope("evidence_redaction", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "approve"),
            fixture.UserId, "Unverified proposed exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1));

        // Act
        var result = await DispatchAsync(fixture, fixture.UserId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldDenyApprovingUnverifiedScopeWithoutAppendGivenLegacyUnverifiedWaiver()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var approver = await SeedMemberAsync(fixture, BuiltInRbac.AdministratorsTeamId(fixture.TenantId));
        var waiverId = Uuid.CreateVersion4();
        // Synthetic historical invalid scope demonstrates the new approval verifier independently of recording.
        await ProgramManagementServices.SeedAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, waiverId), waiver =>
        {
            Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope("evidence_redaction", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "approve"),
                RbacIds.Member(fixture.TenantId, fixture.UserId), RbacIds.Member(fixture.TenantId, fixture.UserId), "Requester",
                "Historical unverified scope", ArtifactMetadataAccessTests.Fixture.Now, ArtifactMetadataAccessTests.Fixture.Now.AddHours(1)));
            return Result.Success;
        });
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, waiverId));

        // Act
        var result = await DispatchAsync(fixture, approver, new ApproveSeparationOfDutiesWaiver(fixture.TenantId, waiverId));
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, waiverId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Null(after.ApprovedAt);
    }

    [Theory]
    [InlineData("original")]
    [InlineData("derived")]
    [InlineData("suspended")]
    [InlineData("permission")]
    [InlineData("attest_history")]
    public async Task ShouldDenyWithoutAppendGivenCurrentPreparationAuthorityRevoked(string fault)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var grants = await GrantPairAsync(fixture, fixture.UserId, derived);
        if (fault is "original" or "derived")
            Assert.True((await fixture.DispatchAsync(new RevokeAccessGrant(fixture.TenantId, grants[fault == "original" ? 0 : 1]))).IsSuccess);
        else if (fault == "suspended")
            await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.TenantId, fixture.UserId),
                member => member.Suspend(RbacIds.Member(fixture.TenantId, fixture.UserId), "Administrator", ArtifactMetadataAccessTests.Fixture.Now, "Paused member"));
        else if (fault == "permission")
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new RolePermission(fixture.TenantId, BuiltInRbac.TenantAdministrationRoleId(fixture.TenantId), RbacPermissions.EvidenceRedactionPrepare), item => item.Remove());
        else
            // Synthetic accepted history exercises the existing denial-only wall, not professional authority.
            await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId, fixture.UserId);
        var request = new PrepareEvidenceRedaction(fixture.TenantId, Uuid.CreateVersion4(), 0,
            fixture.ArtifactId, derived, "Manual", "Private fields");

        // Act
        var result = await DispatchAsync(fixture, fixture.UserId, request);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, request.RedactionId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
        Assert.Empty(retained.Preparations);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("self")]
    [InlineData("stale")]
    [InlineData("unavailable")]
    [InlineData("original")]
    [InlineData("derived")]
    [InlineData("permission")]
    [InlineData("attest_history")]
    public async Task ShouldPreservePreparationWithoutAppendGivenIneligibleApproval(string fault)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture, fault == "unavailable" ? EvidenceInspectionOutcome.Malware : EvidenceInspectionOutcome.Clean);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        var contributor = fault == "self" ? lead : await SeedMemberAsync(fixture, BuiltInRbac.StandardUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, contributor, derived);
        var grants = await GrantPairAsync(fixture, lead, derived);
        var prepared = await DispatchAsync(fixture, contributor, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var preparation = prepared.Value.Preparations[0];
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));
        if (fault is "original" or "derived")
            Assert.True((await fixture.DispatchAsync(new RevokeAccessGrant(fixture.TenantId, grants[fault == "original" ? 0 : 1]))).IsSuccess);
        else if (fault == "permission")
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new RolePermission(fixture.TenantId, BuiltInRbac.ComplianceManagementRoleId(fixture.TenantId), RbacPermissions.EvidenceRedactionApprove), item => item.Remove());
        else if (fault == "attest_history")
            await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId, lead);
        RequestInvocation invocation = fault == "direct" ? new DirectInvocation() : fault == "mcp" ? new McpInvocation("redaction-approve") :
            new HttpInvocation("POST", "/test", "/test", "test");

        // Act
        var result = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId,
            1, fault == "stale" ? Uuid.CreateVersion4() : preparation.PreparationId, 1), invocation);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(before.CurrentPreparation, after.CurrentPreparation);
        Assert.Empty(after.Approvals);
        if (fault == "unavailable")
        {
            var current = await DispatchAsync(fixture, lead, new GetEvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));
            Assert.True(current.IsSuccess, current.Error?.Message);
            Assert.False(current.Value.ApprovalCurrent);
            Assert.Equal("quarantined", current.Value.DerivedState);
        }
    }

    [Theory]
    [InlineData("active")]
    [InlineData("approver_suspended")]
    [InlineData("approver_permission_removed")]
    public async Task ShouldRequireCurrentIndependentWaiverAuthorityGivenLeadSelfApproval(string fault)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        var approver = await SeedMemberAsync(fixture, BuiltInRbac.AdministratorsTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, lead, derived);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, approver, derived);
        var prepared = await DispatchAsync(fixture, lead, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var preparation = prepared.Value.Preparations[0];
        var scope = new SeparationOfDutiesWaiverScope("evidence_redaction", prepared.Value.RedactionId,
            preparation.PreparationId, preparation.Revision, "approve");
        var recorded = await DispatchAsync(fixture, fixture.UserId, new RecordSeparationOfDutiesWaiver(fixture.TenantId,
            scope, lead, "Exact independent exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1)));
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var waived = await DispatchAsync(fixture, approver, new ApproveSeparationOfDutiesWaiver(fixture.TenantId, recorded.Value.WaiverId));
        Assert.True(waived.IsSuccess, waived.Error?.Message);
        if (fault == "approver_suspended")
            await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.TenantId, approver),
                member => member.Suspend(RbacIds.Member(fixture.TenantId, fixture.UserId), "Administrator", ArtifactMetadataAccessTests.Fixture.Now, "Paused approver"));
        else if (fault == "approver_permission_removed")
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new RolePermission(fixture.TenantId, BuiltInRbac.TenantAdministrationRoleId(fixture.TenantId), RbacPermissions.TenantRbacManage), item => item.Remove());
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Act
        var approved = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(fixture.TenantId,
            prepared.Value.RedactionId, 1, preparation.PreparationId, 1, recorded.Value.WaiverId));
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Assert
        if (fault == "active")
        {
            Assert.True(approved.IsSuccess, approved.Error?.Message);
            Assert.True(approved.Value.ApprovalCurrent);
            Assert.True(approved.Value.Approvals[0].SeparationOfDutiesWaived);
            Assert.Equal(recorded.Value.WaiverId, after.CurrentApproval!.SeparationOfDutiesWaiver!.WaiverId);
        }
        else
        {
            Assert.False(approved.IsSuccess);
            Assert.Equal(RequestErrorKind.Forbidden, approved.Error!.Kind);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Empty(after.Approvals);
        }
    }

    [Fact]
    public async Task ShouldReturnCurrentApprovalWithoutAppendGivenExactPreparationRetry()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var contributor = await SeedMemberAsync(fixture, BuiltInRbac.StandardUsersTeamId(fixture.TenantId));
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, contributor, derived);
        await GrantPairAsync(fixture, lead, derived);
        var request = new PrepareEvidenceRedaction(fixture.TenantId, Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields");
        var metadata = RequestMetadata.Create();
        var prepared = await DispatchAsync(fixture, contributor, request, metadata: metadata);
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var preparation = prepared.Value.Preparations[0];
        var approved = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(fixture.TenantId,
            request.RedactionId, 1, preparation.PreparationId, 1));
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, request.RedactionId));

        // Act
        var retried = await DispatchAsync(fixture, contributor, request, metadata: metadata);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, request.RedactionId));

        // Assert
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.True(retried.Value.ApprovalCurrent);
        Assert.Equal(approved.Value.Approvals, retried.Value.Approvals);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(preparation, retried.Value.Preparations[0]);
    }

    static async Task<Uuid> SeedDerivedAsync(ArtifactMetadataAccessTests.Fixture fixture, EvidenceInspectionOutcome outcome = EvidenceInspectionOutcome.Clean)
    {
        var derived = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, derived), artifact =>
            artifact.Register(new EvidenceArtifactContent("Declared derivative", "Manual preparation", "manual", "Existing content",
                    ArtifactMetadataAccessTests.Fixture.Now, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "restricted"),
                new string('b', 64), 30, ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Collector"),
                ArtifactMetadataAccessTests.Fixture.Now));
        // Synthetic inspector fixture: no byte transformation or real scanner proof is claimed.
        await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, derived), artifact =>
        {
            Assert.Null(artifact.RecordInspection(outcome, ArtifactMetadataAccessTests.Fixture.Now));
            return Result.Success;
        });
        return derived;
    }

    static async Task<Uuid> SeedMemberAsync(ArtifactMetadataAccessTests.Fixture fixture, Uuid team)
    {
        var user = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.TenantId, user), member => member.Register());
        var member = await ProgramManagementServices.HydrateAsync(fixture.Provider, new Member(fixture.TenantId, user));
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new TeamMember(fixture.TenantId, team, member.Id), item => item.Assign(member.MembershipEpisodeId));
        await fixture.ProjectAsync();
        return user;
    }

    static async Task<List<Uuid>> GrantPairAsync(ArtifactMetadataAccessTests.Fixture fixture, Uuid user, Uuid derived)
    {
        var grants = new List<Uuid>();
        foreach (var artifact in new[] { fixture.ArtifactId, derived })
        {
            var template = fixture.Grant(artifact, BuiltInRbac.ViewerRoleId(fixture.TenantId));
            var request = template with
            {
                Proposal = template.Proposal with
                {
                    Principal = new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, RbacIds.Member(fixture.TenantId, user))
                }
            };
            Assert.True((await fixture.DispatchAsync(request)).IsSuccess);
            grants.Add(request.GrantId);
        }
        await fixture.ProjectAsync();
        return grants;
    }

    static async Task<Result<T>> DispatchAsync<T>(ArtifactMetadataAccessTests.Fixture fixture, Uuid user, IRequest<T> request,
        RequestInvocation? invocation = null, RequestMetadata? metadata = null)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user),
                invocation ?? new HttpInvocation("POST", "/test", "/test", "test"), metadata));
    }

}
