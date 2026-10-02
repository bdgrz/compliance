using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Only public display text from a unique explicit tenant-local correlation.</summary>
public interface IPersonMemberDisplayReader
{
    ValueTask<string?> ReadAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default);
}
