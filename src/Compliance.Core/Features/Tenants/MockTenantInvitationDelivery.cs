using System.Collections.Concurrent;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Temporary in-process delivery for development and automated verification.</summary>
public sealed class MockTenantInvitationDelivery : ITenantInvitationDelivery
{
    readonly ConcurrentDictionary<(Uuid TenantId, string EmailAddress), string> _messages = new();

    public ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress, string token,
        CancellationToken ct)
    {
        _ = attemptId;
        ct.ThrowIfCancellationRequested();
        _messages[(tenantId, emailAddress)] = token;
        return ValueTask.CompletedTask;
    }

    public bool TryGetLatest(Uuid tenantId, string emailAddress, out string? token) =>
        _messages.TryGetValue((tenantId, emailAddress), out token);
}
