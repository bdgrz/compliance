using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

/// <summary>Unit-level policy proof with production request authorization and local permission observations.</summary>
public sealed class ApplicationRevisionPreviewContractTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("classification")]
    [InlineData("system_owner")]
    [InlineData("access_owner")]
    public async Task ShouldRejectExplicitClearGivenCompleteGovernedPreview(string field)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        var request = fixture.CompleteRequest();
        request = field switch
        {
            "classification" => request with { Classification = null, ClassificationIntent = new(true) },
            "system_owner" => request with { SystemOwnerPersonId = null, SystemOwnerIntent = new(true) },
            "access_owner" => request with { AccessOwnerPersonId = null, AccessOwnerIntent = new(true) },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        // Act
        var result = await fixture.PreviewAsync(request);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReportOwnerClearsGivenExistingPreviewShapeWithDisabledRevisionGate()
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(fixture.Request(), fixture.Actor);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before, await fixture.EventsAsync());
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
        Assert.Contains(result.Value.Changes, change => change.Field == "system_owner_person_id" &&
            change.Before == fixture.Owner.ToString() && change.After is null);
        Assert.Contains(result.Value.Changes, change => change.Field == "access_owner_person_id" &&
            change.Before == fixture.Owner.ToString() && change.After is null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldMatchSingleSavedRevisionGivenSamePreviewContentAndRevision(bool enabled, bool explicitChanges)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();
        var nextOwner = Uuid.CreateVersion4();
        await fixture.RecordPersonAsync(nextOwner);
        var request = explicitChanges ? fixture.CompleteRequest() with
        {
            Name = " Revised payroll ",
            Purpose = " Revised purpose ",
            OwnerReference = " New attribution ",
            Classification = " confidential ",
            SystemOwnerPersonId = nextOwner,
            AccessOwnerPersonId = nextOwner,
            ApplicationKind = " database ",
            Criticality = " high ",
        } : fixture.Request();
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();
        var baseline = await fixture.CurrentAsync();

        // Act
        var preview = await fixture.PreviewAsync(request);
        var saved = await fixture.Bus.SendAsync(new ReviseApplication(request.TenantId,
            request.ApplicationId, request.ExpectedApplicationRevision, request.Name!, request.Purpose!,
            request.OwnerReference, request.Classification, request.SystemOwnerPersonId,
            request.AccessOwnerPersonId, ApplicationKind: request.ApplicationKind,
            Criticality: request.Criticality), fixture.Actor);
        var after = await fixture.EventsAsync();

        // Assert
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(before.Length + 1, after.Length);
        Assert.Equal(before, after.Take(before.Length));
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
        var revised = Assert.IsType<ApplicationRevised>(after[^1].Event);
        Assert.Equal(fixture.Tenant, revised.TenantId);
        Assert.Equal(fixture.Application, revised.ApplicationId);
        Assert.Equal(request.ExpectedApplicationRevision + 1, revised.Revision);
        Assert.Equal(baseline.IsRestricted, revised.IsRestricted);
        Assert.Equal("manual", baseline.SourceKind);
        Assert.Equal(fixture.Application.ToString(), baseline.SourceIdentifier);
        var reconstructed = Content(baseline);
        foreach (var change in preview.Value.Changes)
        {
            Assert.Equal(reconstructed[change.Field], change.Before);
            Assert.NotEqual(change.Before, change.After);
            reconstructed[change.Field] = change.After;
        }
        Assert.Equal(8, reconstructed.Count);
        Assert.Equal(preview.Value.Changes.Count, preview.Value.Changes.Select(change => change.Field).Distinct().Count());
        Assert.Equal(revised.Name, reconstructed["name"]);
        Assert.Equal(revised.Purpose, reconstructed["purpose"]);
        Assert.Equal(revised.OwnerReference, reconstructed["owner_reference"]);
        Assert.Equal(revised.Classification, reconstructed["classification"]);
        Assert.Equal(revised.SystemOwnerPersonId?.ToString(), reconstructed["system_owner_person_id"]);
        Assert.Equal(revised.AccessOwnerPersonId?.ToString(), reconstructed["access_owner_person_id"]);
        Assert.Equal(revised.ApplicationKind, reconstructed["application_kind"]);
        Assert.Equal(revised.Criticality, reconstructed["criticality"]);
        Assert.Equal(explicitChanges ? 8 : enabled ? 1 : 4, preview.Value.Changes.Count);
        Assert.False(preview.Value.Complete);
        Assert.Contains("engagements", preview.Value.PendingContexts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldOmitUnchangedFieldsGivenEqualNormalizedContent(bool enabled)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();

        // Act
        var result = await fixture.PreviewAsync(fixture.CompleteRequest());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldTrimAdditionsBeforeBoundsGivenEitherRevisionMode(bool enabled)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();
        var value = new string('x', 200);

        // Act
        var result = await fixture.PreviewAsync(fixture.CompleteRequest() with
        {
            ApplicationKind = "  " + value + "  ",
            Criticality = "  " + value + "  ",
        });

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["application_kind", "criticality"], result.Value.Changes.Select(change => change.Field));
        Assert.All(result.Value.Changes, change => Assert.Equal(value, change.After));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldResolveClassificationAndAttributionGivenBlankInputs(bool enabled)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();

        // Act
        var result = await fixture.PreviewAsync(fixture.CompleteRequest() with
        {
            Classification = " \t ",
            OwnerReference = " \t ",
        });

        // Assert
        if (enabled)
            Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        else
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(["owner_reference", "classification"], result.Value.Changes.Select(change => change.Field));
            Assert.All(result.Value.Changes, change => Assert.Null(change.After));
        }
    }

    [Fact]
    public async Task ShouldAvoidGuessedMetadataGivenUnresolvedLegacyContentWithDisabledGate()
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync(complete: false);

        // Act
        var result = await fixture.PreviewAsync(fixture.Request());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Changes);
    }

    [Theory]
    [InlineData("classification")]
    [InlineData("system_owner")]
    [InlineData("access_owner")]
    [InlineData("kind")]
    [InlineData("criticality")]
    public async Task ShouldRejectMissingRequiredResultGivenEnabledGateAndLegacyContent(string missing)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync(complete: false);
        var complete = fixture.CompleteRequest();
        var request = missing switch
        {
            "classification" => complete with { Classification = null },
            "system_owner" => complete with { SystemOwnerPersonId = null },
            "access_owner" => complete with { AccessOwnerPersonId = null },
            "kind" => complete with { ApplicationKind = null },
            "criticality" => complete with { Criticality = null },
            _ => throw new ArgumentOutOfRangeException(nameof(missing)),
        };

        // Act
        var result = await fixture.PreviewAsync(request);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData(false, "name_blank")]
    [InlineData(true, "name_blank")]
    [InlineData(false, "name_missing")]
    [InlineData(true, "name_missing")]
    [InlineData(false, "purpose_blank")]
    [InlineData(true, "purpose_blank")]
    [InlineData(false, "purpose_missing")]
    [InlineData(true, "purpose_missing")]
    [InlineData(false, "name_raw_bound")]
    [InlineData(true, "name_raw_bound")]
    [InlineData(false, "purpose_raw_bound")]
    [InlineData(true, "purpose_raw_bound")]
    [InlineData(false, "attribution_raw_bound")]
    [InlineData(true, "attribution_raw_bound")]
    [InlineData(false, "classification_raw_bound")]
    [InlineData(true, "classification_raw_bound")]
    [InlineData(false, "system_owner_empty")]
    [InlineData(true, "system_owner_empty")]
    [InlineData(false, "access_owner_empty")]
    [InlineData(true, "access_owner_empty")]
    [InlineData(false, "kind_blank")]
    [InlineData(true, "kind_blank")]
    [InlineData(false, "criticality_blank")]
    [InlineData(true, "criticality_blank")]
    [InlineData(false, "kind_large")]
    [InlineData(true, "kind_large")]
    [InlineData(false, "criticality_large")]
    [InlineData(true, "criticality_large")]
    public async Task ShouldRejectInvalidFieldsWithoutEventsGivenEitherRevisionMode(bool enabled, string invalid)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();
        var complete = fixture.CompleteRequest();
        var request = invalid switch
        {
            "name_blank" => complete with { Name = " \t " },
            "name_missing" => complete with { Name = null },
            "purpose_blank" => complete with { Purpose = " \t " },
            "purpose_missing" => complete with { Purpose = null },
            "name_raw_bound" => complete with { Name = " " + new string('x', 200) },
            "purpose_raw_bound" => complete with { Purpose = " " + new string('x', 2000) },
            "attribution_raw_bound" => complete with { OwnerReference = " " + new string('x', 2000) },
            "classification_raw_bound" => complete with { Classification = " " + new string('x', 200) },
            "system_owner_empty" => complete with { SystemOwnerPersonId = Uuid.Empty },
            "access_owner_empty" => complete with { AccessOwnerPersonId = Uuid.Empty },
            "kind_blank" => complete with { ApplicationKind = " \t " },
            "criticality_blank" => complete with { Criticality = " \t " },
            "kind_large" => complete with { ApplicationKind = " " + new string('x', 201) + " " },
            "criticality_large" => complete with { Criticality = " " + new string('x', 201) + " " },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid)),
        };

        // Act
        var result = await fixture.PreviewAsync(request);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, true, true)]
    public async Task ShouldRevalidateBothEffectiveOwnersGivenMissingOrForeignPerson(bool enabled,
        bool retained, bool foreign, bool accessOwner)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        var invalidOwner = Uuid.CreateVersion4();
        if (foreign)
            await fixture.RecordPersonAsync(invalidOwner, fixture.ForeignTenant);
        await fixture.SeedAsync(systemOwner: retained && !accessOwner ? invalidOwner : null,
            accessOwner: retained && accessOwner ? invalidOwner : null);
        var request = retained ? fixture.Request() : fixture.CompleteRequest();
        if (!retained)
            request = accessOwner ? request with { AccessOwnerPersonId = invalidOwner }
                : request with { SystemOwnerPersonId = invalidOwner };

        // Act
        var result = await fixture.PreviewAsync(request);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Contains(accessOwner ? "access owner" : "system owner", result.Error?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectEmptyRetainedOwnerGivenEnabledGate(bool accessOwner)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync(systemOwner: accessOwner ? null : Uuid.Empty,
            accessOwner: accessOwner ? Uuid.Empty : null);

        // Act
        var result = await fixture.PreviewAsync(fixture.Request());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData("system_owner")]
    [InlineData("system_owner_empty")]
    [InlineData("access_owner")]
    [InlineData("access_owner_empty")]
    [InlineData("kind")]
    [InlineData("kind_blank")]
    [InlineData("criticality")]
    [InlineData("criticality_blank")]
    public async Task ShouldRejectSuppliedNewFieldsGivenRetirementPreview(string supplied)
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync();
        var retire = new PreviewApplicationChange(fixture.Tenant, fixture.Application, 1, "retire");
        var request = supplied switch
        {
            "system_owner" => retire with { SystemOwnerPersonId = fixture.Owner },
            "system_owner_empty" => retire with { SystemOwnerPersonId = Uuid.Empty },
            "access_owner" => retire with { AccessOwnerPersonId = fixture.Owner },
            "access_owner_empty" => retire with { AccessOwnerPersonId = Uuid.Empty },
            "kind" => retire with { ApplicationKind = "service" },
            "kind_blank" => retire with { ApplicationKind = " " },
            "criticality" => retire with { Criticality = "high" },
            "criticality_blank" => retire with { Criticality = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(supplied)),
        };

        // Act
        var result = await fixture.PreviewAsync(request);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData("inventory_denied", RequestErrorKind.Forbidden)]
    [InlineData("tenant_access_denied", RequestErrorKind.Forbidden)]
    [InlineData("identity_only", RequestErrorKind.Forbidden)]
    [InlineData("program_only", RequestErrorKind.Forbidden)]
    [InlineData("nonmember", RequestErrorKind.NotFound)]
    [InlineData("foreign_tenant", RequestErrorKind.NotFound)]
    [InlineData("foreign_resource", RequestErrorKind.NotFound)]
    [InlineData("restricted", RequestErrorKind.NotFound)]
    [InlineData("stale", RequestErrorKind.Conflict)]
    public async Task ShouldPreserveSourceStreamsGivenProductionPolicyDenialOrStaleRevision(string denial,
        RequestErrorKind expected)
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync(restricted: denial == "restricted");
        fixture.Permissions.InventoryAllowed = denial is not ("inventory_denied" or "identity_only");
        fixture.Permissions.TenantAccessAllowed = denial is not ("tenant_access_denied" or "identity_only" or "program_only");
        fixture.Permissions.ProgramOnly = denial == "program_only";
        fixture.Memberships.State = denial == "nonmember" ? "nonmember" : "active";
        var request = fixture.Request();
        request = denial switch
        {
            "foreign_tenant" => request with { TenantId = fixture.ForeignTenant, ApplicationId = fixture.ForeignApplication },
            "foreign_resource" => request with { ApplicationId = fixture.ForeignApplication },
            "stale" => request with { ExpectedApplicationRevision = 2 },
            _ => request,
        };

        // Act
        var result = await fixture.PreviewAsync(request);

        // Assert
        Assert.Equal(expected, result.Error?.Kind);
        if (denial == "inventory_denied")
            Assert.Contains("inventory", result.Error?.Message);
        if (denial is "tenant_access_denied" or "program_only")
            Assert.Contains("organization-wide program access", result.Error?.Message);
    }

    static Dictionary<string, string?> Content(ApplicationView current) => new()
    {
        ["name"] = current.Name,
        ["purpose"] = current.Purpose,
        ["owner_reference"] = current.OwnerReference,
        ["classification"] = current.Classification,
        ["system_owner_person_id"] = current.SystemOwnerPersonId?.ToString(),
        ["access_owner_person_id"] = current.AccessOwnerPersonId?.ToString(),
        ["application_kind"] = current.ApplicationKind,
        ["criticality"] = current.Criticality,
    };

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _events = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Application { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public Uuid ForeignTenant { get; } = Uuid.CreateVersion4();
        public Uuid ForeignApplication { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor => new(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));
        public Permissions Permissions { get; } = new();
        public ApplicationImportCompositionTests.MembershipDirectory Memberships { get; }
        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();

        public Fixture(bool enabled)
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                    ["Fitz:ApplicationName"] = "compliance",
                    ["Compliance:Applications:GovernedRevisionsEnabled"] = enabled.ToString(),
                }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(_events);
            services.AddSingleton<IDomainEventReader>(_events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(Permissions);
            services.AddSingleton<IAccessGrantScopePermissionAuthorizer>(Permissions);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            Memberships = new(Tenant, User);
            services.AddSingleton<ITenantMembershipDirectoryReader>(Memberships);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public async Task SeedAsync(bool complete = true, Uuid? systemOwner = null,
            Uuid? accessOwner = null, bool restricted = false)
        {
            await RecordPersonAsync(Owner);
            var application = new DeclaredApplication(Tenant, Application);
            Assert.True(application.Declare("Payroll", "Run payroll", complete ? "Finance" : null, User,
                "Original", Now, complete ? "internal" : null,
                complete ? systemOwner ?? Owner : null, complete ? accessOwner ?? Owner : null,
                isRestricted: restricted, applicationKind: complete ? "service" : null,
                criticality: complete ? "critical" : null).IsSuccess);
            await Writer.SaveAsync(application, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            var foreign = new DeclaredApplication(ForeignTenant, ForeignApplication);
            Assert.True(foreign.Declare("Foreign", "Foreign purpose", "Foreign attribution", User,
                "Foreign original", Now, "confidential", Owner, Owner,
                applicationKind: "database", criticality: "high").IsSuccess);
            await Writer.SaveAsync(foreign, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            var directory = _scope.ServiceProvider.GetRequiredService<FitzApplicationDirectory>();
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(Tenant.ToString())),
                ProjectionCheckpoint.Start));
            foreach (var record in await EventsAsync())
                await directory.ApplyAsync(record.Event);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        public async Task RecordPersonAsync(Uuid id, Uuid? tenant = null)
        {
            var person = new Person(tenant ?? Tenant, id);
            Assert.True(person.Record("Owner", null, ActorReference.ForMember(User, "Manager"), Now).IsSuccess);
            await Writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        public PreviewApplicationChange Request() => new(Tenant, Application, 1, "revise", "Payroll", "Run payroll");
        public PreviewApplicationChange CompleteRequest() => Request() with
        {
            Name = " Payroll ",
            Purpose = " Run payroll ",
            OwnerReference = " Finance ",
            Classification = " internal ",
            SystemOwnerPersonId = Owner,
            AccessOwnerPersonId = Owner,
            ApplicationKind = " service ",
            Criticality = " critical ",
        };

        public async Task<Result<ApplicationChangePreview>> PreviewAsync(PreviewApplicationChange request)
        {
            var before = await EventsAsync();
            var foreignBefore = await ForeignEventsAsync();
            var result = await Bus.SendAsync(request, Actor);
            Assert.Equal(before, await EventsAsync());
            Assert.Equal(foreignBefore, await ForeignEventsAsync());
            return result;
        }

        public async Task<ApplicationView> CurrentAsync() =>
            (await _scope.ServiceProvider.GetRequiredService<IApplicationDirectoryReader>().GetAsync(Tenant, Application))!;
        public Task<DomainEventRecord[]> EventsAsync() => EventsAsync(Tenant, Application);
        public Task<DomainEventRecord[]> ForeignEventsAsync() => EventsAsync(ForeignTenant, ForeignApplication);

        async Task<DomainEventRecord[]> EventsAsync(Uuid tenant, Uuid application)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _events.ReadAsync(new EventStreamAddress(tenant.ToString(),
                               "applications", application.ToString()), 0, CancellationToken.None))
                records.Add(record);
            return records.ToArray();
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Permissions : IPermissionAuthorizer, IAccessGrantPermissionAuthorizer,
        IAccessGrantScopePermissionAuthorizer
    {
        public bool InventoryAllowed { get; set; } = true;
        public bool TenantAccessAllowed { get; set; } = true;
        public bool ProgramOnly { get; set; }
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(InventoryAllowed && permission == RbacPermissions.ApplicationInventoryManage);
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default)
        {
            HashSet<Uuid> programs = ProgramOnly ? [Uuid.CreateVersion4()] : [];
            return ValueTask.FromResult(new ProgramAccessVisibility(
                TenantAccessAllowed && permission == "tenant.access", programs));
        }
        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(false);
        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
