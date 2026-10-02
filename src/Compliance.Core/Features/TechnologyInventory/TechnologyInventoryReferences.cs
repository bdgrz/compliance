using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Resolves cross-record references from the authoritative event-sourced records.</summary>
public sealed class TechnologyInventoryReferences(IAggregateReader reader,
    IApplicationInventoryActivity applications, IApplicationDirectoryReader instances)
    : ITechnologyInventoryActivity
{
    public async ValueTask<RequestError?> ValidateOwnerAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct) =>
        personId == Uuid.Empty ||
        (await reader.HydrateAsync(new Person(tenantId, personId), ct).ConfigureAwait(false))
        .IsCreated
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "The owner must be a recorded workforce person in this tenant.");

    /// <summary>Requires a declared <c>cloud_account</c> system instance in this tenant.</summary>
    public async ValueTask<RequestError?> ValidateCloudAccountAsync(Uuid tenantId,
        Uuid? systemInstanceId, CancellationToken ct)
    {
        if (systemInstanceId is not { } id || id == Uuid.Empty)
            return null;
        var state = await applications.GetInstanceStateAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (state == SystemInstanceReferenceState.Pending)
            return new RequestError(RequestErrorKind.Conflict,
                "The system instance has not finished projecting. Retry the command.",
                isTransient: true);
        var instance = state == SystemInstanceReferenceState.Declared
            ? await instances.GetInstanceAsync(tenantId, id, ct).ConfigureAwait(false)
            : null;
        return instance is { Kind: TechnologyInventoryRules.CloudAccount }
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "A cloud_account component requires a declared cloud_account system instance in this tenant.");
    }

    /// <summary>Resolves a flow's endpoints and assets, returning the highest carried classification.</summary>
    public async ValueTask<Result<string>> ResolveFlowAsync(Uuid tenantId,
        string? sourceType, Uuid sourceId, string? destinationType, Uuid? destinationId,
        IReadOnlyList<Uuid>? assetIds, CancellationToken ct)
    {
        switch (sourceType?.Trim())
        {
            case TechnologyInventoryRules.ComponentReference:
                if ((await ActiveComponentAsync(tenantId, sourceId, ct).ConfigureAwait(false))
                    is null)
                    return Invalid("The flow source must be an active component in this tenant.");
                break;
            case TechnologyInventoryRules.SystemInstanceReference:
                var state = await applications.GetInstanceStateAsync(tenantId, sourceId, ct)
                    .ConfigureAwait(false);
                if (state == SystemInstanceReferenceState.Pending)
                    return Result<string>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The source system instance has not finished projecting. Retry the command.",
                        isTransient: true));
                if (state != SystemInstanceReferenceState.Declared)
                    return Invalid("The flow source must be a declared system instance in this tenant.");
                break;
        }
        if (destinationType?.Trim() == TechnologyInventoryRules.ComponentReference &&
            destinationId is { } destination &&
            (await ActiveComponentAsync(tenantId, destination, ct).ConfigureAwait(false))
            ?.Category != TechnologyInventoryRules.DataStore)
            return Invalid("The flow destination must be an active data_store component in this tenant.");
        var classification = "public";
        foreach (var assetId in (assetIds ?? []).Distinct())
        {
            var asset = await reader.HydrateAsync(new InformationAsset(tenantId, assetId), ct)
                .ConfigureAwait(false);
            if (asset.Content is not { Lifecycle: TechnologyInventoryRules.Active } content)
                return Invalid("Every carried information asset must be active in this tenant.");
            if (TechnologyInventoryRules.Rank(content.Classification) >
                TechnologyInventoryRules.Rank(classification))
                classification = content.Classification;
        }
        return Result<string>.Success(classification);
    }

    /// <summary>Checks that a governed boundary scope subject is an active inventory record.</summary>
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, string subjectType, Uuid id,
        CancellationToken ct = default) => subjectType switch
        {
            "component" => (await ActiveComponentAsync(tenantId, id, ct).ConfigureAwait(false))
                is not null,
            "information" => (await reader.HydrateAsync(new InformationAsset(tenantId, id), ct)
                .ConfigureAwait(false)).Content is { Lifecycle: TechnologyInventoryRules.Active },
            "location" => (await reader.HydrateAsync(new LocationRegister(tenantId), ct)
                .ConfigureAwait(false)).Get(id) is { Lifecycle: TechnologyInventoryRules.Active },
            "process" => (await reader.HydrateAsync(new OperationalProcessRegister(tenantId), ct)
                .ConfigureAwait(false)).Get(id) is { Lifecycle: TechnologyInventoryRules.Active },
            "data_flow" => (await reader.HydrateAsync(new DataFlow(tenantId, id), ct)
                .ConfigureAwait(false)).Content is { Lifecycle: TechnologyInventoryRules.Active },
            _ => false,
        };

    async ValueTask<TechnologyComponentContent?> ActiveComponentAsync(Uuid tenantId, Uuid id,
        CancellationToken ct) =>
        (await reader.HydrateAsync(new TechnologyComponent(tenantId, id), ct)
            .ConfigureAwait(false)).Content is { Lifecycle: TechnologyInventoryRules.Active } content
            ? content
            : null;

    static Result<string> Invalid(string message) =>
        Result<string>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
