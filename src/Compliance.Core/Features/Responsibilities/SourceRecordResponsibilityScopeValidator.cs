using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

/// <summary>Routes responsibility scope validation to the feature that owns the record type.</summary>
sealed class SourceRecordResponsibilityScopeValidator(
    BoundaryResponsibilityScopeValidator boundaries,
    ControlResponsibilityScopeValidator controls,
    CommitmentResponsibilityScopeValidator commitments) : IResponsibilityScopeValidator
{
    public ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.RecordType switch
        {
            SeparationOfDutiesRecordTypes.Boundary => boundaries.ValidateAsync(tenantId, scope, ct),
            SeparationOfDutiesRecordTypes.Control => controls.ValidateAsync(tenantId, scope, ct),
            SeparationOfDutiesRecordTypes.Commitment => commitments.ValidateAsync(tenantId, scope, ct),
            _ => ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The responsibility record type is not supported."))),
        };
    }
}
