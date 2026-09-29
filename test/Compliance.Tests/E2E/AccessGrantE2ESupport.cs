using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.E2E;

static class AccessGrantE2ESupport
{
    public static Task<Uuid> IssueFounderOrganizationGrantAsync(HttpClient client, Guid tenantId) =>
        IssueFounderOrganizationGrantAsync(client, tenantId.ToString());

    public static Task<Uuid> IssueFounderOrganizationGrantAsync(HttpClient client, string tenantId) =>
        IssueFounderOrganizationGrantAsync(client, Uuid.Parse(tenantId, CultureInfo.InvariantCulture));

    public static Task<Uuid> IssueFounderOrganizationGrantForProgramPathAsync(HttpClient client,
        string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tenantIndex = Array.IndexOf(segments, "tenants");
        Assert.True(tenantIndex >= 0 && tenantIndex + 2 < segments.Length &&
            segments[tenantIndex + 2] == "programs", "The path must identify a tenant's programs.");
        return IssueFounderOrganizationGrantAsync(client, segments[tenantIndex + 1]);
    }

    public static async Task<Uuid> IssueFounderOrganizationGrantAsync(HttpClient client, Uuid tenantId)
    {
        using var session = await client.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        using var document = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        var userId = Uuid.Parse(document.RootElement.GetProperty("id").GetString()!,
            CultureInfo.InvariantCulture);
        var grantId = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenantId}/access-grants/{grantId}";
        var listPath = $"/api/v1/tenants/{tenantId}/access-grants";
        using (var existing = await client.GetAsync(listPath))
        {
            if (existing.StatusCode == HttpStatusCode.OK)
            {
                using var grants = JsonDocument.Parse(await existing.Content.ReadAsStringAsync());
                foreach (var grant in grants.RootElement.GetProperty("grants").EnumerateArray())
                {
                    var terms = grant.GetProperty("terms");
                    if (terms.GetProperty("source").GetProperty("id").GetString() == "founder-setup" &&
                        terms.GetProperty("principal").GetProperty("id").GetString() ==
                        RbacIds.Member(tenantId, userId).ToString() &&
                        (!grant.TryGetProperty("revoked_at", out var revoked) ||
                         revoked.ValueKind == JsonValueKind.Null))
                        return Uuid.Parse(grant.GetProperty("grant_id").GetString()!,
                            CultureInfo.InvariantCulture);
                }
            }
        }
        var proposal = new
        {
            principal = new { kind = "member", id = RbacIds.Member(tenantId, userId).ToString() },
            role_id = BuiltInRbac.TenantAdministrationRoleId(tenantId).ToString(),
            scope = new { kind = "organization", id = tenantId.ToString() },
            source = new { kind = "manual", id = "founder-setup" },
            effective_from = DateTimeOffset.UtcNow.AddMinutes(-1),
            effective_until = (DateTimeOffset?)null,
        };

        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        string? lastResponse = null;
        var succeeded = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var issued = await client.PostAsJsonAsync(path, new { proposal });
            if (issued.IsSuccessStatusCode)
            {
                succeeded = true;
                break;
            }
            lastResponse = $"{(int)issued.StatusCode} {await issued.Content.ReadAsStringAsync()}";
            Assert.True(issued.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                lastResponse);
            await Task.Delay(250);
        }

        Assert.True(succeeded,
            $"The founder could not issue organization grant {grantId} in tenant {tenantId}: " +
            lastResponse);

        var projectionDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
        string? lastProjection = null;
        while (DateTimeOffset.UtcNow < projectionDeadline)
        {
            using var listed = await client.GetAsync(listPath);
            if (listed.StatusCode == HttpStatusCode.OK)
            {
                using var grants = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
                var items = grants.RootElement.GetProperty("grants").EnumerateArray();
                if (items.Any(grant =>
                        grant.GetProperty("grant_id").GetString() == grantId.ToString()))
                    return grantId;
                lastProjection = $"200 OK; {items.Count()} grant(s) projected";
            }
            else
                lastProjection = $"{(int)listed.StatusCode} {await listed.Content.ReadAsStringAsync()}";
            await Task.Delay(250);
        }

        throw new TimeoutException($"Founder organization grant {grantId} in tenant {tenantId} " +
            $"did not reach the projection; last GET: {lastProjection}");
    }
}
