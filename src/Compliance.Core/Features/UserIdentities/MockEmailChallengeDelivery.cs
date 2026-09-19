using System.Collections.Concurrent;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Temporary in-process email service for development and automated verification.</summary>
public sealed class MockEmailChallengeDelivery : IEmailChallengeDelivery
{
    readonly ConcurrentDictionary<(Uuid UserId, string EmailAddress), string> _messages = new();

    public ValueTask SendAsync(Uuid userId, string emailAddress, string token, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _messages[(userId, emailAddress)] = token;
        return ValueTask.CompletedTask;
    }

    public bool TryGetLatest(Uuid userId, string emailAddress, out string? token) =>
        _messages.TryGetValue((userId, emailAddress), out token);
}
