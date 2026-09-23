using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class EmailVerificationE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldRejectReservationGivenAddressOwnedByAnotherUser()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var ownedEmail = $"owned-{Guid.NewGuid():N}@example.com";

        // Act
        using var firstLogin = await firstClient.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument(ownedEmail));

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
        using var firstSession = await firstClient.GetAsync("/auth/session");
        var first = await firstSession.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(first);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var reserved = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await firstClient.GetAsync(
                $"/api/v1/users/{first.Id}/email-addresses/{ownedEmail}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                reserved = true;
                break;
            }
            await Task.Delay(250);
        }
        Assert.True(reserved);

        using var secondLogin = await secondClient.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument($"other-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        using var secondSession = await secondClient.GetAsync("/auth/session");
        var second = await secondSession.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(second);

        using var conflict = await secondClient.PostAsync(
            $"/api/v1/users/{second.Id}/email-addresses/{ownedEmail}", null);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ShouldReserveAndVerifyAddressGivenMockDelivery()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var client = factory.CreateClient();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        // Act
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new RegistrationDocument(email));

        // Assert
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await client.GetAsync("/auth/session");
        var identity = await session.Content.ReadFromJsonAsync<SessionDocument>();
        Assert.NotNull(identity);

        var path = $"/api/v1/users/{identity.Id}/email-addresses/{email}";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        HttpResponseMessage? get = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            get?.Dispose();
            get = await client.GetAsync(path);
            if (get.StatusCode == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.NotNull(get);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var reserved = await get.Content.ReadFromJsonAsync<EmailDocument>();
        Assert.NotNull(reserved);
        Assert.False(reserved.Verified);
        get.Dispose();

        using var forbidden = await client.PostAsync(
            $"/api/v1/users/{Guid.NewGuid()}/email-addresses/{email}/challenges", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenList = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}/email-addresses");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenList.StatusCode);
        using var forbiddenStatus = await client.GetAsync(
            $"/api/v1/users/{Guid.NewGuid()}/email-addresses/{email}/challenges/status");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenStatus.StatusCode);

        using var issued = await client.PostAsync($"{path}/challenges", null);
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var delivery = factory.Services.GetRequiredService<MockEmailChallengeDelivery>();
        string? token = null;
        var deliveryDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deliveryDeadline &&
               !delivery.TryGetLatest(Uuid.Parse(identity.Id, CultureInfo.InvariantCulture),
                   email, out token))
            await Task.Delay(250);
        Assert.NotNull(token);

        using var status = await client.GetAsync($"{path}/challenges/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var statusBody = await status.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, statusBody, StringComparison.Ordinal);
        Assert.DoesNotContain(email, statusBody, StringComparison.Ordinal);

        using var completed = await client.PostAsJsonAsync($"{path}/verifications", new { token });
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        EmailDocument? verified = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                verified = await response.Content.ReadFromJsonAsync<EmailDocument>();
                if (verified?.Verified == true)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.True(verified?.Verified);
        using var verifiedSession = await client.GetAsync("/auth/session");
        var sessionState = await verifiedSession.Content.ReadFromJsonAsync<VerifiedSessionDocument>();
        Assert.True(sessionState?.EmailAddressVerified);

        var secondEmail = $"second-{Guid.NewGuid():N}@example.com";
        var secondPath = $"/api/v1/users/{identity.Id}/email-addresses/{secondEmail}";
        using var reservedSecond = await client.PostAsync(secondPath, null);
        Assert.Equal(HttpStatusCode.NoContent, reservedSecond.StatusCode);
        using var issuedSecond = await client.PostAsync($"{secondPath}/challenges", null);
        Assert.Equal(HttpStatusCode.NoContent, issuedSecond.StatusCode);
        string? secondToken = null;
        var secondDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < secondDeadline &&
               !delivery.TryGetLatest(Uuid.Parse(identity.Id, CultureInfo.InvariantCulture),
                   secondEmail, out secondToken))
            await Task.Delay(250);
        Assert.NotNull(secondToken);
        using var completedSecond = await client.PostAsJsonAsync(
            $"{secondPath}/verifications", new { token = secondToken });
        Assert.Equal(HttpStatusCode.NoContent, completedSecond.StatusCode);

        EmailListDocument? addresses = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/v1/users/{identity.Id}/email-addresses");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                addresses = await response.Content.ReadFromJsonAsync<EmailListDocument>();
                if (addresses?.Items.Count == 2 && addresses.Items.All(item => item.Verified))
                    break;
            }
            await Task.Delay(250);
        }
        Assert.NotNull(addresses);
        Assert.Equal(2, addresses.Items.Count);
        Assert.All(addresses.Items, item => Assert.True(item.Verified));
    }

    sealed record RegistrationDocument([property: JsonPropertyName("email_address")] string EmailAddress);
    sealed record SessionDocument(string Id);
    sealed record VerifiedSessionDocument(
        [property: JsonPropertyName("email_address_verified")] bool EmailAddressVerified);
    sealed record EmailDocument(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("email_address")] string EmailAddress,
        bool Verified);
    sealed record EmailListDocument(IReadOnlyList<EmailDocument> Items);
}
