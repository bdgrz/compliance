using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Fitz;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
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

        var metadata = RequestMetadata.Create();
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, metadata.RequestId));

        // Act
        var result = await DispatchAsync(fixture, fixture.UserId, request, metadata: metadata);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, metadata.RequestId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(0UL, after.CommittedStreamPosition);
        Assert.False(after.IsRecorded);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyWithoutAppendGivenWaiverAdminPermissionRemovedDuringArtifactRead(bool approval)
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
        var record = new RecordSeparationOfDutiesWaiver(fixture.TenantId,
            new SeparationOfDutiesWaiverScope("evidence_redaction", prepared.Value.RedactionId, preparation.PreparationId, 1, "approve"),
            lead, "Exact independent exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1));
        var metadata = RequestMetadata.Create();
        var waiverId = metadata.RequestId;
        if (approval)
        {
            var recorded = await DispatchAsync(fixture, fixture.UserId, record, metadata: metadata);
            Assert.True(recorded.IsSuccess, recorded.Error?.Message);
            waiverId = recorded.Value.WaiverId;
        }
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, waiverId));
        fixture.Faults.OnArtifactRead = () => ProgramManagementServices.SeedAsync(fixture.Provider,
            new RolePermission(fixture.TenantId, BuiltInRbac.TenantAdministrationRoleId(fixture.TenantId), RbacPermissions.TenantRbacManage), item => item.Remove());

        // Act
        var result = approval ? await DispatchAsync(fixture, approver, new ApproveSeparationOfDutiesWaiver(fixture.TenantId, waiverId)) :
            await DispatchAsync(fixture, fixture.UserId, record, metadata: metadata);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, waiverId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Null(after.ApprovedAt);
    }

    [Fact]
    public async Task ShouldDenyWithoutAppendGivenWaiverExpiresDuringFinalApproverPermissionRead()
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
        var recorded = await DispatchAsync(fixture, fixture.UserId, new RecordSeparationOfDutiesWaiver(fixture.TenantId,
            new SeparationOfDutiesWaiverScope("evidence_redaction", prepared.Value.RedactionId, preparation.PreparationId, 1, "approve"),
            lead, "Exact independent exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1)));
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True((await DispatchAsync(fixture, approver, new ApproveSeparationOfDutiesWaiver(fixture.TenantId, recorded.Value.WaiverId))).IsSuccess);
        var clock = new MutableClock { Now = ArtifactMetadataAccessTests.Fixture.Now };
        await using var provider = Compose(fixture, clock, approver);
        await using var scope = provider.CreateAsyncScope();
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ApproveEvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId, 1, preparation.PreparationId, 1, recorded.Value.WaiverId),
            new RequestDispatchContext(ProgramManagementServices.Actor(lead), new HttpInvocation("POST", "/test", "/test", "test")));
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Empty(after.Approvals);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("mcp")]
    [InlineData("waiver")]
    public async Task ShouldReturnSameDenialWithoutAppendGivenAbsentOrUnreadableLineage(string mode)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var outsider = await SeedMemberAsync(fixture, BuiltInRbac.AdministratorsTeamId(fixture.TenantId));
        RequestInvocation invocation = mode == "mcp" ? new McpInvocation("redaction-get") : new HttpInvocation("GET", "/test", "/test", "test");
        var absentId = Uuid.CreateVersion4();
        var existingMetadata = RequestMetadata.Create();
        var absentMetadata = RequestMetadata.Create();
        var preparation = prepared.Value.Preparations[0];

        // Act
        RequestError? existingError;
        RequestError? absentError;
        if (mode == "waiver")
        {
            var existing = await DispatchAsync(fixture, outsider, new RecordSeparationOfDutiesWaiver(fixture.TenantId,
                new SeparationOfDutiesWaiverScope("evidence_redaction", prepared.Value.RedactionId, preparation.PreparationId, 1, "approve"),
                fixture.UserId, "Exact exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1)), metadata: existingMetadata);
            var absent = await DispatchAsync(fixture, outsider, new RecordSeparationOfDutiesWaiver(fixture.TenantId,
                new SeparationOfDutiesWaiverScope("evidence_redaction", absentId, preparation.PreparationId, 1, "approve"),
                fixture.UserId, "Exact exception", ArtifactMetadataAccessTests.Fixture.Now.AddHours(1)), metadata: absentMetadata);
            existingError = existing.Error;
            absentError = absent.Error;
        }
        else
        {
            var existing = await DispatchAsync(fixture, outsider, new GetEvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId), invocation);
            var absent = await DispatchAsync(fixture, outsider, new GetEvidenceRedaction(fixture.TenantId, absentId), invocation);
            existingError = existing.Error;
            absentError = absent.Error;
        }
        var existingWaiver = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, existingMetadata.RequestId));
        var absentWaiver = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, absentMetadata.RequestId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, existingError?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, absentError?.Kind);
        Assert.Equal(absentError!.Message, existingError!.Message);
        Assert.Equal(0UL, existingWaiver.CommittedStreamPosition);
        Assert.Equal(0UL, absentWaiver.CommittedStreamPosition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyApprovalWithoutAppendGivenAvailabilityFactAfterDecisionClock(bool release)
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture, release ? EvidenceInspectionOutcome.Malware : EvidenceInspectionOutcome.Clean,
            ArtifactMetadataAccessTests.Fixture.Now.AddHours(1), release);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var preparation = prepared.Value.Preparations[0];
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Act
        var result = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(fixture.TenantId,
            prepared.Value.RedactionId, 1, preparation.PreparationId, 1));
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Empty(after.Approvals);
    }

    [Fact]
    public async Task ShouldRefuseApplicabilityGivenRetainedApprovalSourcePositionChanged()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(fixture.TenantId,
            Uuid.CreateVersion4(), 0, fixture.ArtifactId, derived, "Manual", "Private fields"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var approved = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(fixture.TenantId,
            prepared.Value.RedactionId, 1, prepared.Value.Preparations[0].PreparationId, 1));
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        var clock = new MutableClock { Now = ArtifactMetadataAccessTests.Fixture.Now };
        // Replays the genuine retained event envelope with only the private claimed source position corrupted.
        await using var provider = Compose(fixture, clock, Uuid.Empty,
            new ChangedApprovalStore(fixture.Provider.GetRequiredService<IEventStore>(), prepared.Value.RedactionId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new GetEvidenceRedaction(fixture.TenantId, prepared.Value.RedactionId),
            new RequestDispatchContext(ProgramManagementServices.Actor(lead), new HttpInvocation("GET", "/test", "/test", "test")));

        // Assert
        Assert.True(!result.IsSuccess || !result.Value.ApprovalCurrent);
    }

    sealed class ChangedApprovalStore(IEventStore inner, Uuid redactionId) : IEventStore
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                yield return Change(record);
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                yield return Change(record);
        }

        DomainEventRecord Change(DomainEventRecord record)
        {
            if (record.Event is not EvidenceRedactionApproved ev || ev.RedactionId != redactionId)
                return record;
            var changed = ev with { Approval = ev.Approval with { DerivedSourcePosition = 999 } };
            changed.AttachMetadata(ev.Metadata);
            return record with { Event = changed };
        }

        public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition, IReadOnlyList<DomainEvent> events,
            CancellationToken ct = default) => inner.AppendAsync(stream, expectedStreamPosition, events, ct);
    }

    static ServiceProvider Compose(ArtifactMetadataAccessTests.Fixture fixture, MutableClock clock, Uuid approver, IEventStore? events = null)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        services.AddSingleton(events ?? fixture.Provider.GetRequiredService<IEventStore>());
        services.AddSingleton<IDomainEventReader>(events ?? fixture.Provider.GetRequiredService<IDomainEventReader>());
        services.AddSingleton(fixture.Provider.GetRequiredService<IKvClient>());
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<IPermissionAuthorizer>(provider =>
            new AdvancingPermissionAuthorizer(provider.GetRequiredService<FitzPermissionAuthorizer>(), approver, clock));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Delegates every permission decision to the real authoritative implementation; changes only the test clock.
    sealed class AdvancingPermissionAuthorizer(IPermissionAuthorizer inner, Uuid approver, MutableClock clock) : IPermissionAuthorizer
    {
        public async ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            var allowed = await inner.IsAllowedAsync(tenantId, userId, memberId, permission, ct);
            if (permission == RbacPermissions.TenantRbacManage && userId == approver)
                clock.Now = ArtifactMetadataAccessTests.Fixture.Now.AddHours(2);
            return allowed;
        }
    }

    static async Task<Uuid> SeedDerivedAsync(ArtifactMetadataAccessTests.Fixture fixture, EvidenceInspectionOutcome outcome = EvidenceInspectionOutcome.Clean, DateTimeOffset? availableAt = null, bool release = false)
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
            Assert.Null(artifact.RecordInspection(outcome, release ? ArtifactMetadataAccessTests.Fixture.Now : availableAt ?? ArtifactMetadataAccessTests.Fixture.Now));
            return Result.Success;
        });
        if (release)
            await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, derived), artifact =>
            {
                Assert.Null(artifact.ReleaseQuarantine("Synthetic false positive", ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Operator fixture"),
                    availableAt ?? ArtifactMetadataAccessTests.Fixture.Now));
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
