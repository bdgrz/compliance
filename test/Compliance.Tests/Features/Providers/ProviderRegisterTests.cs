using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderRegisterTests
{
    [Fact]
    public void ShouldRetainOriginalDecisionGivenHydratedRetryAfterRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        var now = DateTimeOffset.UtcNow;
        string[] basis = ["customer_data"];
        var initial = new ProviderContent("Example", "Supplier", "material", basis, "Processes data");
        var register = new ProviderRegister(tenantId);
        Assert.True(register.Record(id, requestId, initial, author, now).IsSuccess);
        basis[0] = "spend";
        var revisionId = Uuid.CreateVersion4();
        var revised = initial with { Name = "Renamed", MaterialityBasis = ["customer_data"] };
        Assert.True(register.Revise(id, revisionId, 1, revised, author, now.AddMinutes(1)).IsSuccess);
        var retained = new AggregateScenario<ProviderRegister>(register).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<ProviderRegister>(new ProviderRegister(tenantId)).Given(retained).Aggregate;

        // Act
        var retry = hydrated.Record(id, requestId, initial with { MaterialityBasis = ["customer_data"] }, author, now.AddDays(1));
        var revisionRetry = hydrated.Revise(id, revisionId, 1, revised, author, now.AddDays(1));
        var conflictingRetry = hydrated.Record(id, requestId, initial with { Name = "Different", MaterialityBasis = ["customer_data"] }, author, now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(1, retry.Value!.Revision);
        Assert.True(revisionRetry.IsSuccess);
        Assert.Equal(2, revisionRetry.Value!.Revision);
        Assert.Equal(RequestErrorKind.Conflict, conflictingRetry.Error!.Kind);
        Assert.Empty(new AggregateScenario<ProviderRegister>(hydrated).PendingEvents);
        Assert.Equal("Renamed", hydrated.Get(id)!.Content.Name);
        Assert.Equal(2, hydrated.Get(id)!.Revision);
        Assert.Equal(["customer_data"], Assert.IsType<ProviderRecorded>(retained[0]).Content.MaterialityBasis);
    }

    [Fact]
    public void ShouldRejectDuplicateNameAndStaleRevisionGivenExistingProviders()
    {
        // Arrange
        var register = new ProviderRegister(Uuid.CreateVersion4());
        var id = Uuid.CreateVersion4();
        var otherId = Uuid.CreateVersion4();
        var author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        var now = DateTimeOffset.UtcNow;
        Assert.True(register.Record(id, Uuid.CreateVersion4(), new ProviderContent("Example", "Supplier"), author, now).IsSuccess);
        Assert.True(register.Record(otherId, Uuid.CreateVersion4(), new ProviderContent("Other", "Supplier"), author, now).IsSuccess);

        // Act
        var duplicate = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(), new ProviderContent(" example ", "Host"), author, now);
        var rename = register.Revise(otherId, Uuid.CreateVersion4(), 1, new ProviderContent("EXAMPLE", "Host"), author, now);
        var stale = register.Revise(id, Uuid.CreateVersion4(), 0, new ProviderContent("Renamed", "Host"), author, now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, rename.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Contains("Current revision: 1", stale.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, new AggregateScenario<ProviderRegister>(register).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectOversizedDeclarationGivenBoundedDependencyFields()
    {
        // Arrange
        var dependencies = Enumerable.Range(0, 100).Select(index => new ProviderDependency(
            "client_service", null, null, null, new string('r', 2000), DateTimeOffset.UtcNow,
            UnresolvedReference: $"Service {index}")).ToArray();
        var register = new ProviderRegister(Uuid.CreateVersion4());

        // Act
        var result = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new ProviderContent("Example", "Supplier", Dependencies: dependencies),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("payload", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(new AggregateScenario<ProviderRegister>(register).PendingEvents);
    }

    [Theory]
    [InlineData("public", true)]
    [InlineData("internal", true)]
    [InlineData("confidential", false)]
    [InlineData("restricted", false)]
    [InlineData("", false)]
    [InlineData("unknown", false)]
    public void ShouldLimitCitationMetadataGivenAuthoredClassification(string classification,
        bool allowed)
    {
        // Arrange
        var citation = new ProviderSourceCitation("policy", "Supplier list", "2026-10",
            "section 2", classification);
        var register = new ProviderRegister(Uuid.CreateVersion4());

        // Act
        var result = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new ProviderContent("Example", "Supplier", SourceCitation: citation),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
            Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldExposeUnresolvedFactsGivenIncompleteSubserviceDeclaration()
    {
        // Arrange
        var register = new ProviderRegister(Uuid.CreateVersion4());
        var requested = new ProviderContent("Cloud", "Host", Subservice: true,
            OwnerReference: "Service owner in source");

        // Act
        var result = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(), requested,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        var ev = Assert.IsType<ProviderRecorded>(Assert.Single(
            new AggregateScenario<ProviderRegister>(register).PendingEvents));
        Assert.Equal("carve_out", ev.Content.BoundaryTreatment);
        Assert.Null(ev.Content.OwnerPersonId);
        Assert.Equal("Service owner in source", ev.Content.OwnerReference);
        Assert.Equal(["materiality", "owner", "source_citation", "csocs"],
            ProviderRules.Unresolved(ev.Content));
    }

    [Theory]
    [InlineData("inclusive", null)]
    [InlineData("unknown", "Reason")]
    public void ShouldRejectUnreasonedSubserviceTreatmentGivenExceptionDeclaration(
        string treatment, string? rationale)
    {
        // Arrange
        var register = new ProviderRegister(Uuid.CreateVersion4());

        // Act
        var result = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new ProviderContent("Cloud", "Host", Subservice: true, BoundaryTreatment: treatment,
                BoundaryTreatmentRationale: rationale),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ProviderRegister>(register).PendingEvents);
    }

    [Fact]
    public void ShouldRejectEmptyEffectiveIntervalGivenDeclaredDependency()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dependency = new ProviderDependency("client_service", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), null, "Depends on provider", now, now);
        var register = new ProviderRegister(Uuid.CreateVersion4());

        // Act
        var result = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new ProviderContent("Example", "Supplier", Dependencies: [dependency]),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData("not_material", "customer_data")]
    [InlineData("not_material", "critical_path")]
    [InlineData("unknown", "critical_path")]
    [InlineData("material", "spend")]
    public void ShouldRejectContradictoryMaterialityGivenAuthoredClassification(
        string materiality, string basis)
    {
        // Arrange
        var register = new ProviderRegister(Uuid.CreateVersion4());

        // Act
        var result = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new ProviderContent("Example", "Supplier", materiality, [basis], "Authored basis"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ProviderRegister>(register).PendingEvents);
    }

    [Fact]
    public void ShouldRetainAttributedDeclarationGivenManualProvider()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        var recordedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var register = new ProviderRegister(tenantId);

        // Act
        var result = register.Record(providerId, requestId, new ProviderContent(" Example ",
            " Supplier "), actor, recordedAt);

        // Assert
        Assert.True(result.IsSuccess);
        var ev = Assert.IsType<ProviderRecorded>(Assert.Single(
            new AggregateScenario<ProviderRegister>(register).PendingEvents));
        Assert.Equal(tenantId, ev.TenantId);
        Assert.Equal(providerId, ev.ProviderId);
        Assert.Equal(requestId, ev.RequestId);
        Assert.Equal("Example", ev.Content.Name);
        Assert.Equal("Supplier", ev.Content.ProviderKind);
        Assert.Equal(actor, ev.Actor);
        Assert.Equal(recordedAt, ev.RecordedAt);
    }
}
