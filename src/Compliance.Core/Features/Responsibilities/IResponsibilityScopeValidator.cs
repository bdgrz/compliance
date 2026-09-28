using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

/// <summary>Validates responsibility scopes through the feature that owns the source record.</summary>
public interface IResponsibilityScopeValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default);
}
