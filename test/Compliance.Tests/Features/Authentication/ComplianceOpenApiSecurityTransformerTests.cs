using Bdgrz.Compliance.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bdgrz.Compliance.Tests.Features.Authentication;

public sealed class ComplianceOpenApiSecurityTransformerTests
{
    [Fact]
    public async Task ShouldDescribeCookieAndBearerGivenApiUserAuthentication()
    {
        // Arrange
        var document = new OpenApiDocument();
        var transformer = new ComplianceOpenApiSecurityTransformer();

        // Act
        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);

        // Assert
        var schemes = Assert.IsType<OpenApiComponents>(document.Components).SecuritySchemes!;
        var cookie = Assert.IsType<OpenApiSecurityScheme>(schemes["BdgrzSessionCookie"]);
        Assert.Equal(SecuritySchemeType.ApiKey, cookie.Type);
        Assert.Equal(ParameterLocation.Cookie, cookie.In);
        Assert.Equal("bdgrz_session", cookie.Name);
        var bearer = Assert.IsType<OpenApiSecurityScheme>(schemes["BdgrzBearer"]);
        Assert.Equal(SecuritySchemeType.Http, bearer.Type);
        Assert.Equal("bearer", bearer.Scheme);
        Assert.Equal("JWT", bearer.BearerFormat);
        Assert.Null(document.Security);
    }

    [Fact]
    public async Task ShouldRequireCookieOrBearerGivenApiUserOperation()
    {
        // Arrange
        var document = new OpenApiDocument();
        var operation = new OpenApiOperation();
        var transformer = new ComplianceOpenApiSecurityTransformer();

        // Act
        await transformer.TransformAsync(operation, OperationContext(document,
            new AuthorizeAttribute("BdgrzApiUser")), CancellationToken.None);

        // Assert
        var requirements = Assert.IsType<List<OpenApiSecurityRequirement>>(operation.Security);
        Assert.Equal(2, requirements.Count);
        Assert.Equal(["BdgrzBearer", "BdgrzSessionCookie"], requirements.Select(requirement =>
            Assert.Single(requirement).Key.Reference.Id).Order(StringComparer.Ordinal));
        Assert.All(requirements, requirement => Assert.Empty(Assert.Single(requirement).Value));
    }

    [Fact]
    public async Task ShouldLeaveAnonymousOperationGivenAnonymousOverride()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var transformer = new ComplianceOpenApiSecurityTransformer();

        // Act
        await transformer.TransformAsync(operation, OperationContext(new OpenApiDocument(),
            new AuthorizeAttribute("BdgrzApiUser"), new AllowAnonymousAttribute()), CancellationToken.None);

        // Assert
        Assert.Null(operation.Security);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("BdgrzSession")]
    [InlineData("OtherPolicy")]
    public async Task ShouldLeaveOtherContractGivenOperationWithoutApiUserPolicy(string? policy)
    {
        // Arrange
        var operation = new OpenApiOperation();
        var transformer = new ComplianceOpenApiSecurityTransformer();
        var metadata = policy is null ? Array.Empty<object>() : [new AuthorizeAttribute(policy)];

        // Act
        await transformer.TransformAsync(operation, OperationContext(new OpenApiDocument(), metadata), CancellationToken.None);

        // Assert
        Assert.Null(operation.Security);
    }

    [Fact]
    public async Task ShouldPreserveExplicitSecurityGivenOperationWithExistingRequirement()
    {
        // Arrange
        var document = new OpenApiDocument();
        var requirement = new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Custom", document)] = [] };
        var operation = new OpenApiOperation { Security = [requirement] };
        var transformer = new ComplianceOpenApiSecurityTransformer();

        // Act
        await transformer.TransformAsync(operation, OperationContext(document,
            new AuthorizeAttribute("BdgrzApiUser")), CancellationToken.None);

        // Assert
        Assert.Same(requirement, Assert.Single(operation.Security));
    }

    [Fact]
    public async Task ShouldRetainUnrelatedSchemeGivenExistingDocumentComponents()
    {
        // Arrange
        var original = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, Name = "custom", In = ParameterLocation.Header };
        var document = new OpenApiDocument { Components = new OpenApiComponents { SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme> { ["Custom"] = original } } };
        var transformer = new ComplianceOpenApiSecurityTransformer();

        // Act
        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);

        // Assert
        Assert.Equal(3, document.Components.SecuritySchemes.Count);
        Assert.Same(original, document.Components.SecuritySchemes["Custom"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldStopTransformGivenCancelledGeneration(bool documentTransform)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var transformer = new ComplianceOpenApiSecurityTransformer();
        var document = new OpenApiDocument();
        var operation = new OpenApiOperation();

        // Act
        var transform = () => documentTransform
            ? transformer.TransformAsync(document, DocumentContext(), cancellation.Token)
            : transformer.TransformAsync(operation, OperationContext(document,
                new AuthorizeAttribute("BdgrzApiUser")), cancellation.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(transform);
        Assert.Null(document.Components);
        Assert.Null(operation.Security);
    }

    static OpenApiDocumentTransformerContext DocumentContext() => new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = new EmptyServices(),
    };

    static OpenApiOperationTransformerContext OperationContext(OpenApiDocument document, params object[] metadata) => new()
    {
        DocumentName = "v1",
        Description = new ApiDescription { ActionDescriptor = new ActionDescriptor { EndpointMetadata = metadata.ToList() } },
        ApplicationServices = new EmptyServices(),
        Document = document,
    };

    sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
